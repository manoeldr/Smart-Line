namespace SmartLine.Iot.Simulacao;

/// <summary>Como as paradas e as quedas de comunicação automáticas do simulador acontecem.</summary>
/// <param name="RodandoMedio">Tempo médio produzindo entre uma parada e outra (sorteado entre 50% e 150% dele).</param>
/// <param name="ParadaMinima">Duração mínima de uma parada.</param>
/// <param name="ParadaMaxima">Duração máxima de uma parada.</param>
/// <param name="IncluirDesligado">Se há quedas de comunicação automáticas (rede caindo ou WISE sem energia).</param>
/// <param name="QuedaMedia">Tempo médio comunicando entre uma queda e outra (sorteado entre 50% e 150%); padrão 60 min.</param>
/// <param name="QuedaMinima">Duração mínima de uma queda; padrão 3 min (mais que os 90 s do sistema).</param>
/// <param name="QuedaMaxima">Duração máxima de uma queda; padrão 10 min.</param>
public sealed record OpcoesParadasAleatorias(
    TimeSpan RodandoMedio,
    TimeSpan ParadaMinima,
    TimeSpan ParadaMaxima,
    bool IncluirDesligado = true,
    TimeSpan? QuedaMedia = null,
    TimeSpan? QuedaMinima = null,
    TimeSpan? QuedaMaxima = null)
{
    public static OpcoesParadasAleatorias Padrao { get; } =
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

    public TimeSpan EntreQuedas => QuedaMedia ?? TimeSpan.FromMinutes(60);
    public TimeSpan QuedaMin => QuedaMinima ?? TimeSpan.FromMinutes(3);
    public TimeSpan QuedaMax => QuedaMaxima ?? TimeSpan.FromMinutes(10);
}

/// <summary>A comunicação de uma máquina mudou (caiu ou voltou) no automático.</summary>
/// <param name="Falha">A falha que começou; nula quando a comunicação voltou.</param>
/// <param name="Ate">Até quando fica assim (próxima mudança).</param>
public sealed record MudancaComunicacao(FalhaComunicacao? Falha, DateTime Ate);

/// <summary>
/// Paradas aleatórias e esporádicas para o simulador: cada máquina ligada no
/// automático produz por um tempo sorteado, para por um motivo sorteado (os
/// mesmos cenários dos comandos manuais) por um tempo sorteado e volta a
/// produzir, e assim por diante. À parte, num relógio próprio e mais espaçado,
/// a comunicação cai (a rede, com o WISE contando; ou o WISE sem energia, que
/// volta com os contadores zerados) por alguns minutos e volta.
/// </summary>
/// <remarks>
/// Não é thread-safe: quem usa (o simulador) chama sempre sob a mesma trava
/// com que muda o cenário das máquinas pelos comandos.
/// </remarks>
public sealed class ParadasAleatorias
{
    // Peso de cada tipo de parada no sorteio: as causadas por sensor são as mais comuns.
    private static readonly (CenarioSimulado Cenario, int Peso)[] Pesos =
    [
        (CenarioSimulado.FaltaGarrafas, 30),
        (CenarioSimulado.SaidaGarrafasBloqueada, 20),
        (CenarioSimulado.ParadaSemCausa, 20),
        (CenarioSimulado.AbaixoAcumuloMinimo, 15),
        (CenarioSimulado.SaidaCaixasBloqueada, 10),
    ];

    // Queda de comunicação: na maioria das vezes é a rede (o WISE continua contando).
    private const int PercentualSemEnergia = 25;

    private readonly OpcoesParadasAleatorias _opcoes;
    private readonly Random _aleatorio;
    private readonly Dictionary<int, DateTime> _proximaMudanca = new(); // por número da máquina
    private readonly Dictionary<int, DateTime> _proximaComunicacao = new(); // queda ou volta, por máquina

    /// <param name="aleatorio">Fixe a semente nos testes; nulo = sorteio de verdade.</param>
    public ParadasAleatorias(OpcoesParadasAleatorias opcoes, Random? aleatorio = null)
    {
        if (opcoes.RodandoMedio <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "O tempo médio rodando deve ser maior que zero.");
        if (opcoes.ParadaMinima <= TimeSpan.Zero || opcoes.ParadaMaxima < opcoes.ParadaMinima)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "A duração da parada deve ser maior que zero, com mínima até a máxima.");
        if (opcoes.EntreQuedas <= TimeSpan.Zero || opcoes.QuedaMin <= TimeSpan.Zero || opcoes.QuedaMax < opcoes.QuedaMin)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "O tempo entre quedas e a duração delas devem ser maiores que zero, com mínima até a máxima.");

        _opcoes = opcoes;
        _aleatorio = aleatorio ?? Random.Shared;
    }

    public OpcoesParadasAleatorias Opcoes => _opcoes;

    public bool Ligado(MaquinaSimulada maquina) => _proximaMudanca.ContainsKey(maquina.Numero);

    /// <summary>Quando a máquina muda de novo (para ou volta a rodar); nulo se não está no automático.</summary>
    public DateTime? ProximaMudanca(MaquinaSimulada maquina) =>
        _proximaMudanca.TryGetValue(maquina.Numero, out var quando) ? quando : null;

    /// <summary>Quando a comunicação cai ou volta; nulo sem quedas automáticas.</summary>
    public DateTime? ProximaMudancaComunicacao(MaquinaSimulada maquina) =>
        _proximaComunicacao.TryGetValue(maquina.Numero, out var quando) ? quando : null;

    /// <summary>Põe a máquina no automático a partir do cenário em que ela está.</summary>
    public void Ligar(MaquinaSimulada maquina, DateTime agoraUtc)
    {
        _proximaMudanca[maquina.Numero] = agoraUtc + (maquina.Cenario == CenarioSimulado.Rodando ? TempoRodando() : TempoParada());
        if (_opcoes.IncluirDesligado)
            _proximaComunicacao[maquina.Numero] = agoraUtc + (maquina.Falha is null ? TempoEntreQuedas() : TempoQueda());
    }

    /// <summary>
    /// Tira a máquina do automático (ela fica no cenário em que está). Uma queda de
    /// comunicação automática em curso termina aqui: sem o automático, ninguém a faria voltar.
    /// </summary>
    public void Desligar(MaquinaSimulada maquina, DateTime? agoraUtc = null)
    {
        _proximaMudanca.Remove(maquina.Numero);
        if (_proximaComunicacao.Remove(maquina.Numero) && maquina.Falha is not null)
            maquina.RecuperarComunicacao(agoraUtc ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Se chegou a hora, derruba ou devolve a comunicação da máquina e diz o que mudou; senão, nulo.
    /// </summary>
    public MudancaComunicacao? VerificarComunicacao(MaquinaSimulada maquina, DateTime agoraUtc)
    {
        if (!_proximaComunicacao.TryGetValue(maquina.Numero, out var quando) || agoraUtc < quando)
            return null;

        if (maquina.Falha is null)
        {
            var falha = _aleatorio.Next(100) < PercentualSemEnergia ? FalhaComunicacao.SemEnergia : FalhaComunicacao.SemRede;
            maquina.PerderComunicacao(falha, agoraUtc);
            _proximaComunicacao[maquina.Numero] = agoraUtc + TempoQueda();
        }
        else
        {
            maquina.RecuperarComunicacao(agoraUtc);
            _proximaComunicacao[maquina.Numero] = agoraUtc + TempoEntreQuedas();
        }

        return new MudancaComunicacao(maquina.Falha, _proximaComunicacao[maquina.Numero]);
    }

    /// <summary>
    /// Se chegou a hora, muda o cenário da máquina (rodando → parada sorteada,
    /// parada → rodando) e devolve o novo cenário; senão, nulo.
    /// </summary>
    public CenarioSimulado? Verificar(MaquinaSimulada maquina, DateTime agoraUtc)
    {
        if (!_proximaMudanca.TryGetValue(maquina.Numero, out var quando) || agoraUtc < quando)
            return null;

        if (maquina.Cenario == CenarioSimulado.Rodando)
        {
            maquina.MudarCenario(SortearParada(), agoraUtc);
            _proximaMudanca[maquina.Numero] = agoraUtc + TempoParada();
        }
        else
        {
            maquina.MudarCenario(CenarioSimulado.Rodando, agoraUtc);
            _proximaMudanca[maquina.Numero] = agoraUtc + TempoRodando();
        }

        return maquina.Cenario;
    }

    private CenarioSimulado SortearParada()
    {
        var opcoes = Pesos;
        var sorteio = _aleatorio.Next(opcoes.Sum(p => p.Peso));
        foreach (var (cenario, peso) in opcoes)
        {
            if (sorteio < peso)
                return cenario;
            sorteio -= peso;
        }
        return opcoes[^1].Cenario;
    }

    private TimeSpan TempoRodando() =>
        TimeSpan.FromTicks((long)(_opcoes.RodandoMedio.Ticks * (0.5 + _aleatorio.NextDouble())));

    private TimeSpan TempoEntreQuedas() =>
        TimeSpan.FromTicks((long)(_opcoes.EntreQuedas.Ticks * (0.5 + _aleatorio.NextDouble())));

    private TimeSpan TempoQueda() =>
        _opcoes.QuedaMin + TimeSpan.FromTicks((long)((_opcoes.QuedaMax - _opcoes.QuedaMin).Ticks * _aleatorio.NextDouble()));

    private TimeSpan TempoParada() =>
        _opcoes.ParadaMinima + TimeSpan.FromTicks((long)((_opcoes.ParadaMaxima - _opcoes.ParadaMinima).Ticks * _aleatorio.NextDouble()));
}
