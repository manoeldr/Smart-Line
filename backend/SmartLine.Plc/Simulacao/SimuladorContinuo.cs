using SmartLine.Core.Plc;

namespace SmartLine.Plc.Simulacao;

/// <summary>
/// Simula uma máquina que produz e para sozinha, evoluindo conforme o relógio.
///
/// Diferente do <see cref="SimuladorRoteiro"/>, não é determinístico por passo:
/// serve para deixar o sistema rodando por horas e ver produção, paradas e OEE
/// aparecendo nas telas sem nenhum PLC. Com semente fixa o comportamento é
/// reproduzível.
/// </summary>
/// <remarks>
/// <para>
/// A simulação é dirigida pelo <b>tempo</b>, não pelo número de chamadas: cada
/// leitura avança o estado até o instante atual. Isso significa que mudar o
/// intervalo de polling não muda o que a máquina produziu — só a resolução com
/// que você observa. Se fosse por chamada, polling mais rápido geraria mais
/// paletes, o que não faz sentido nenhum.
/// </para>
/// <para>
/// Para simular rollover do contador, basta iniciar
/// <see cref="OpcoesSimuladorContinuo.ContadorInicial"/> perto de
/// <see cref="uint.MaxValue"/>: a virada acontece naturalmente.
/// </para>
/// </remarks>
public sealed class SimuladorContinuo : IPlcDataSource
{
    /// <summary>
    /// Piso de duração de qualquer período. Sem isto, um sorteio próximo de
    /// zero faria o laço de avanço girar sem sair do lugar.
    /// </summary>
    private static readonly TimeSpan DuracaoMinima = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Teto de transições processadas numa única leitura. Só é atingido se o
    /// simulador ficar muitíssimo tempo sem ser lido.
    /// </summary>
    private const int MaxTransicoesPorLeitura = 10_000;

    private readonly OpcoesSimuladorContinuo _opcoes;
    private readonly TimeProvider _tempo;
    private readonly Random _sorteio;
    private readonly double _pesoTotal;

    private bool _iniciado;
    private bool _descartado;

    private EstadoWs _estadoAtual = EstadoWs.Operating;
    private uint _codigoFalhaAtual;
    private DateTime _fimDoEstadoUtc;
    private DateTime _ultimoAvancoUtc;

    private double _paletesAcumulados;
    private double _horasOperacao;

    /// <param name="opcoes">Perfil da máquina simulada. Validado no construtor.</param>
    /// <param name="tempo">
    /// Fonte de tempo. Em teste, um <c>FakeTimeProvider</c> permite simular
    /// horas de produção em milissegundos.
    /// </param>
    public SimuladorContinuo(
        OpcoesSimuladorContinuo? opcoes = null,
        TimeProvider? tempo = null)
    {
        _opcoes = opcoes ?? new OpcoesSimuladorContinuo();
        _opcoes.Validar();

        _tempo = tempo ?? TimeProvider.System;
        _sorteio = _opcoes.Semente is { } semente ? new Random(semente) : new Random();
        _pesoTotal = _opcoes.Paradas.Sum(p => p.Peso);
    }

    /// <summary>Estado em que a máquina simulada se encontra. Exposto para asserção em teste.</summary>
    public EstadoWs EstadoAtual => _estadoAtual;

    /// <inheritdoc />
    public Task<LeituraPlc> LerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_descartado, this);
        cancellationToken.ThrowIfCancellationRequested();

        var agora = _tempo.GetUtcNow().UtcDateTime;
        Avancar(agora);

        return Task.FromResult(MontarLeitura(agora));
    }

    // -----------------------------------------------------------------
    // Motor
    // -----------------------------------------------------------------

    /// <summary>Evolui a simulação até <paramref name="agora"/>.</summary>
    private void Avancar(DateTime agora)
    {
        if (!_iniciado)
        {
            Iniciar(agora);
            return;
        }

        // Relógio parado ou andando para trás: nada a fazer.
        if (agora <= _ultimoAvancoUtc)
            return;

        var transicoes = 0;

        while (_fimDoEstadoUtc <= agora && transicoes++ < MaxTransicoesPorLeitura)
        {
            AcumularAte(_fimDoEstadoUtc);
            Transicionar(_fimDoEstadoUtc);
        }

        AcumularAte(agora);
    }

    private void Iniciar(DateTime agora)
    {
        _iniciado = true;
        _ultimoAvancoUtc = agora;

        _estadoAtual = EstadoWs.Operating;
        _codigoFalhaAtual = 0;
        _fimDoEstadoUtc = agora + SortearDuracao(_opcoes.TempoMedioEntreParadas);
    }

    /// <summary>
    /// Credita produção e tempo de operação do trecho percorrido.
    /// Só acumula quando o estado é <see cref="EstadoWs.Operating"/>.
    /// </summary>
    private void AcumularAte(DateTime instante)
    {
        var decorrido = instante - _ultimoAvancoUtc;

        if (decorrido <= TimeSpan.Zero)
            return;

        if (_estadoAtual == EstadoWs.Operating)
        {
            _paletesAcumulados += _opcoes.PaletesPorHora * decorrido.TotalHours;
            _horasOperacao += decorrido.TotalHours;
        }

        _ultimoAvancoUtc = instante;
    }

    /// <summary>Operando vira uma parada sorteada; parada volta a operar.</summary>
    private void Transicionar(DateTime instante)
    {
        if (_estadoAtual == EstadoWs.Operating)
        {
            var parada = SortearParada();

            _estadoAtual = parada.Estado;
            _codigoFalhaAtual = parada.CodigoFalha;
            _fimDoEstadoUtc = instante + SortearDuracao(parada.DuracaoMedia);
        }
        else
        {
            _estadoAtual = EstadoWs.Operating;
            _codigoFalhaAtual = 0;
            _fimDoEstadoUtc = instante + SortearDuracao(_opcoes.TempoMedioEntreParadas);
        }
    }

    /// <summary>Escolhe um tipo de parada proporcionalmente ao peso configurado.</summary>
    private ParadaSimulada SortearParada()
    {
        var alvo = _sorteio.NextDouble() * _pesoTotal;
        var acumulado = 0.0;

        foreach (var parada in _opcoes.Paradas)
        {
            acumulado += parada.Peso;

            if (alvo <= acumulado)
                return parada;
        }

        // Só alcançável por arredondamento de ponto flutuante no último item.
        return _opcoes.Paradas[^1];
    }

    /// <summary>
    /// Sorteia uma duração em torno da média, com distribuição exponencial —
    /// muitos períodos curtos, alguns bem longos, que é como falhas se
    /// comportam na prática.
    /// </summary>
    private TimeSpan SortearDuracao(TimeSpan media)
    {
        // NextDouble() devolve [0,1); 1-u fica em (0,1], nunca zero, então o log é seguro.
        var fator = -Math.Log(1.0 - _sorteio.NextDouble());
        var segundos = media.TotalSeconds * fator;

        // Teto de 10x a média: evita uma parada de dias por azar no sorteio.
        segundos = Math.Min(segundos, media.TotalSeconds * 10);

        var duracao = TimeSpan.FromSeconds(segundos);

        return duracao < DuracaoMinima ? DuracaoMinima : duracao;
    }

    // -----------------------------------------------------------------
    // Leitura
    // -----------------------------------------------------------------

    private LeituraPlc MontarLeitura(DateTime agora)
    {
        var operando = _estadoAtual == EstadoWs.Operating;

        return LeituraPlc.Zerada(agora) with
        {
            CodigoModo = _opcoes.CodigoModo,
            CodigoPrograma = _opcoes.CodigoPrograma,
            CodigoEstado = (uint)_estadoAtual,

            VelocidadeAtual = operando ? _opcoes.VelocidadeProjetadaUnidadesPorMinuto : 0f,

            // Fiel à paletizadora real: a tag existe mas não é preenchida.
            VelocidadeSetada = 0f,

            VelocidadeProjetada = _opcoes.VelocidadeProjetadaUnidadesPorMinuto,

            CodigoFalha = _codigoFalhaAtual,
            TotalPaletes = ContadorAtual(),
            HorasOperacao = (float)_horasOperacao
        };
    }

    /// <summary>
    /// Contador acumulado, com virada natural em <see cref="uint.MaxValue"/> —
    /// é o que permite exercitar o tratamento de rollover.
    /// </summary>
    private uint ContadorAtual()
    {
        var produzidos = (ulong)Math.Floor(_paletesAcumulados);
        var total = _opcoes.ContadorInicial + produzidos;

        return unchecked((uint)(total & 0xFFFF_FFFF));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _descartado = true;
        return ValueTask.CompletedTask;
    }
}