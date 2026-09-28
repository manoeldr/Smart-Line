using SmartLine.Core.Coleta;

namespace SmartLine.Core.Iot;

/// <summary>Situação da máquina vista pela coleta.</summary>
public enum SituacaoMaquina
{
    /// <summary>Coleta iniciada, nenhuma mensagem recebida ainda.</summary>
    AguardandoPrimeiraAmostra,
    Rodando,
    Parada,
    /// <summary>O WISE parou de publicar. Não é parada da máquina.</summary>
    SemComunicacao
}

/// <summary>
/// Estado de UMA máquina acompanhada por WISE: transforma amostras em eventos
/// de produção, parada e comunicação.
/// </summary>
/// <remarks>
/// <para>
/// <b>Puro.</b> Não lê relógio, não acessa banco, não conhece MQTT. O tempo
/// entra pelos parâmetros (<see cref="Processar"/> usa o instante da amostra,
/// <see cref="Verificar"/> recebe o "agora" de quem chama). Isso é o que deixa
/// testar horas de operação em milissegundos e reproduzir qualquer cenário.
/// </para>
/// <para>
/// <b>Não é thread-safe.</b> Quem usa garante uma chamada por vez por máquina
/// (fila por máquina no serviço de coleta).
/// </para>
/// <para>
/// <b>Parada</b> = uma amostra mostra a soma dos contadores de produção marcados
/// sem incremento há <see cref="ConfiguracaoColetaIot.TempoDeteccaoParada"/>.
/// Só amostra confirma parada: se o WISE silenciou, não há evidência de que a
/// máquina parou, e isso vira falta de comunicação (sem parada fantasma). O
/// início é o instante do último incremento, não o da confirmação, então a
/// duração não depende do intervalo de publicação. O fim é o instante da
/// amostra que mostrou produção de novo.
/// </para>
/// <para>
/// <b>Classificação</b> é refeita a cada amostra e a cada verificação enquanto
/// a máquina está parada; se mudar, sai <see cref="ParadaReclassificada"/>.
/// </para>
/// <para>
/// <b>Sem comunicação</b> fecha a parada em curso no instante da última
/// mensagem e não conta como parada. Na volta, a diferença dos contadores
/// credita a produção feita no intervalo.
/// </para>
/// </remarks>
public sealed class EstadoMaquinaIot
{
    private readonly ConfiguracaoColetaIot _config;
    private readonly Dictionary<CanalWise, uint> _contadores = new();
    private readonly Dictionary<CanalWise, bool> _estados = new();

    private DateTime _ultimaAmostraUtc;
    private DateTime _ultimoIncrementoUtc;
    private DateTime _inicioParadaUtc;
    private readonly DateTime? _inicioColetaUtc;

    /// <summary>Situação atual.</summary>
    public SituacaoMaquina Situacao { get; private set; } = SituacaoMaquina.AguardandoPrimeiraAmostra;

    /// <summary>Classificação da parada em curso; nulo fora de parada.</summary>
    public ClassificacaoParada? ClassificacaoAtual { get; private set; }

    /// <summary>Início da parada em curso; nulo fora de parada.</summary>
    public DateTime? InicioParadaUtc => Situacao == SituacaoMaquina.Parada ? _inicioParadaUtc : null;

    /// <summary>Instante da última amostra aceita; nulo antes da primeira.</summary>
    public DateTime? UltimaAmostraUtc =>
        Situacao == SituacaoMaquina.AguardandoPrimeiraAmostra ? null : _ultimaAmostraUtc;

    /// <summary>Último valor bruto de cada sensor de estado lido (para a tela ao vivo).</summary>
    public IReadOnlyDictionary<CanalWise, bool> EstadosSensores => _estados;

    /// <summary>
    /// Último valor bruto de cada contador. O serviço grava isto junto da
    /// consolidação para, após reiniciar, retomar com <c>contadoresRestaurados</c>.
    /// </summary>
    public IReadOnlyDictionary<CanalWise, uint> ContadoresBrutos => _contadores;

    /// <param name="config">Configuração da medição.</param>
    /// <param name="contadoresRestaurados">
    /// Últimos valores brutos conhecidos (backend reiniciado com a coleta em
    /// andamento). Com eles, a primeira amostra já gera a produção feita
    /// enquanto o backend esteve fora. Sem eles, a primeira amostra só vira
    /// referência.
    /// </param>
    /// <param name="semComunicacaoDesdeUtc">
    /// Começa já sem comunicação desde este instante (backend reiniciado: o
    /// período fora do ar foi registrado como sem comunicação). A primeira
    /// amostra emite <see cref="ComunicacaoRestabelecida"/>, que fecha esse período.
    /// </param>
    /// <param name="inicioColetaUtc">
    /// Quando a coleta foi iniciada. Com ele, se nenhuma mensagem chegar em
    /// <see cref="ConfiguracaoColetaIot.TempoSemComunicacao"/>, a comunicação é
    /// dada como perdida desde o início (WISE desligado, IP errado, rede fora).
    /// Sem ele, a coleta espera a primeira mensagem indefinidamente.
    /// </param>
    /// <exception cref="ArgumentException">Instante não está em UTC.</exception>
    public EstadoMaquinaIot(
        ConfiguracaoColetaIot config,
        IReadOnlyDictionary<CanalWise, uint>? contadoresRestaurados = null,
        DateTime? semComunicacaoDesdeUtc = null,
        DateTime? inicioColetaUtc = null)
    {
        _config = config;
        if (inicioColetaUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Instante precisa estar em UTC.", nameof(inicioColetaUtc));
        _inicioColetaUtc = inicioColetaUtc;
        if (semComunicacaoDesdeUtc is { } desde)
        {
            if (desde.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Instante precisa estar em UTC.", nameof(semComunicacaoDesdeUtc));
            Situacao = SituacaoMaquina.SemComunicacao;
            _ultimaAmostraUtc = desde;
        }

        if (contadoresRestaurados is not null)
        {
            foreach (var (canal, valor) in contadoresRestaurados)
            {
                if (_config.Multiplicadores.ContainsKey(canal))
                    _contadores[canal] = valor;
            }
        }
    }

    /// <summary>Processa uma mensagem do WISE.</summary>
    /// <returns>Eventos em ordem cronológica; vazio se nada mudou.</returns>
    /// <exception cref="ArgumentException">Timestamp não está em UTC.</exception>
    public IReadOnlyList<EventoColeta> Processar(AmostraWise amostra)
    {
        if (amostra.TimestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp da amostra precisa estar em UTC.", nameof(amostra));

        var eventos = new List<EventoColeta>();
        var t = amostra.TimestampUtc;

        if (Situacao != SituacaoMaquina.AguardandoPrimeiraAmostra && t < _ultimaAmostraUtc)
            return eventos; // fora de ordem: a mais nova já foi considerada

        // Só comunicação aqui. A detecção de parada por tempo fica para depois de
        // olhar os contadores: esta amostra pode justamente mostrar produção.
        VerificarComunicacao(t, eventos);

        var retomando = Situacao is SituacaoMaquina.AguardandoPrimeiraAmostra or SituacaoMaquina.SemComunicacao;
        if (Situacao == SituacaoMaquina.SemComunicacao)
            eventos.Add(new ComunicacaoRestabelecida(t));

        _ultimaAmostraUtc = t;

        foreach (var (canal, bruto) in amostra.Estados)
        {
            if (_config.SensoresLidos.Contains(canal))
                _estados[canal] = bruto;
        }

        long garrafas = 0;
        long rejeito = 0;
        var produziu = false;
        foreach (var (canal, bruto) in amostra.Contadores)
        {
            if (!_config.Multiplicadores.TryGetValue(canal, out var multiplicador))
                continue; // canal não marcado na medição

            if (_contadores.TryGetValue(canal, out var anterior))
            {
                if (bruto >= anterior)
                {
                    var incremento = (long)(bruto - anterior) * multiplicador;
                    if (MapaWise.Definicao(canal).EhContadorProducao)
                    {
                        garrafas += incremento;
                        produziu |= incremento > 0;
                    }
                    else
                    {
                        rejeito += incremento;
                    }
                }
                else
                {
                    eventos.Add(new ContadorReiniciado(t, canal.ToString(), anterior, bruto));
                }
            }

            _contadores[canal] = bruto;
        }

        if (garrafas > 0 || rejeito > 0)
            eventos.Add(new ProducaoApurada(t, garrafas, rejeito));

        if (retomando)
        {
            // Sem histórico para afirmar que está parada: conta a partir daqui.
            Situacao = SituacaoMaquina.Rodando;
            ClassificacaoAtual = null;
            _ultimoIncrementoUtc = t;
        }

        if (produziu)
        {
            _ultimoIncrementoUtc = t;
            if (Situacao == SituacaoMaquina.Parada)
            {
                eventos.Add(new ParadaEncerrada(t));
                Situacao = SituacaoMaquina.Rodando;
                ClassificacaoAtual = null;
            }
        }

        VerificarParada(t, eventos);
        return eventos;
    }

    /// <summary>
    /// Avança o tempo sem mensagem nova: detecta perda de comunicação e
    /// reclassifica paradas por tempo. Não abre parada (só amostra abre). O
    /// serviço chama periodicamente (ex.: a cada segundo).
    /// </summary>
    /// <param name="agoraUtc">Instante atual, em UTC.</param>
    /// <exception cref="ArgumentException">Instante não está em UTC.</exception>
    public IReadOnlyList<EventoColeta> Verificar(DateTime agoraUtc)
    {
        if (agoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Instante precisa estar em UTC.", nameof(agoraUtc));

        var eventos = new List<EventoColeta>();
        VerificarComunicacao(agoraUtc, eventos);
        VerificarParada(agoraUtc, eventos);
        return eventos;
    }

    /// <summary>
    /// Encerra a coleta (finalização manual): fecha a parada em curso, se houver.
    /// Depois disso a instância não deve mais ser usada.
    /// </summary>
    public IReadOnlyList<EventoColeta> Encerrar(DateTime agoraUtc)
    {
        var eventos = new List<EventoColeta>();
        if (Situacao == SituacaoMaquina.Parada)
        {
            eventos.Add(new ParadaEncerrada(agoraUtc));
            Situacao = SituacaoMaquina.Rodando;
            ClassificacaoAtual = null;
        }
        return eventos;
    }

    private void VerificarComunicacao(DateTime agora, List<EventoColeta> eventos)
    {
        if (Situacao == SituacaoMaquina.AguardandoPrimeiraAmostra)
        {
            // Nunca chegou nada: sem comunicação desde o início da coleta.
            if (_inicioColetaUtc is { } inicio && agora - inicio > _config.TempoSemComunicacao)
            {
                eventos.Add(new ComunicacaoPerdida(inicio));
                Situacao = SituacaoMaquina.SemComunicacao;
                _ultimaAmostraUtc = inicio;
            }
            return;
        }

        if (Situacao == SituacaoMaquina.SemComunicacao)
            return;

        if (agora - _ultimaAmostraUtc <= _config.TempoSemComunicacao)
            return;

        if (Situacao == SituacaoMaquina.Parada)
            eventos.Add(new ParadaEncerrada(_ultimaAmostraUtc));

        eventos.Add(new ComunicacaoPerdida(_ultimaAmostraUtc));
        Situacao = SituacaoMaquina.SemComunicacao;
        ClassificacaoAtual = null;
    }

    private void VerificarParada(DateTime agora, List<EventoColeta> eventos)
    {
        // Medido até a última amostra, não até "agora": sem amostra nova não há evidência.
        if (Situacao == SituacaoMaquina.Rodando
            && _ultimaAmostraUtc - _ultimoIncrementoUtc >= _config.TempoDeteccaoParada)
        {
            _inicioParadaUtc = _ultimoIncrementoUtc;
            var classificacao = Classificar(agora);
            Situacao = SituacaoMaquina.Parada;
            ClassificacaoAtual = classificacao;
            eventos.Add(new ParadaIniciada(_inicioParadaUtc, classificacao));
        }
        else if (Situacao == SituacaoMaquina.Parada)
        {
            var classificacao = Classificar(agora);
            if (classificacao != ClassificacaoAtual)
            {
                ClassificacaoAtual = classificacao;
                eventos.Add(new ParadaReclassificada(agora, classificacao));
            }
        }
    }

    private ClassificacaoParada Classificar(DateTime agora) =>
        MotorRegras.Classificar(_config.Regras, new ContextoParada(_estados, agora - _inicioParadaUtc));
}
