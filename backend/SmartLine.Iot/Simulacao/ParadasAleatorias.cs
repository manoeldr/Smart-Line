namespace SmartLine.Iot.Simulacao;

/// <summary>Como as paradas automáticas do simulador acontecem.</summary>
/// <param name="RodandoMedio">Tempo médio produzindo entre uma parada e outra (sorteado entre 50% e 150% dele).</param>
/// <param name="ParadaMinima">Duração mínima de uma parada.</param>
/// <param name="ParadaMaxima">Duração máxima de uma parada.</param>
/// <param name="IncluirDesligado">Se entre as paradas sorteadas entra o WISE sem energia (sem comunicação).</param>
public sealed record OpcoesParadasAleatorias(
    TimeSpan RodandoMedio,
    TimeSpan ParadaMinima,
    TimeSpan ParadaMaxima,
    bool IncluirDesligado = true)
{
    public static OpcoesParadasAleatorias Padrao { get; } =
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
}

/// <summary>
/// Paradas aleatórias e esporádicas para o simulador: cada máquina ligada no
/// automático produz por um tempo sorteado, para por um motivo sorteado (os
/// mesmos cenários dos comandos manuais) por um tempo sorteado e volta a
/// produzir, e assim por diante.
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
        (CenarioSimulado.Desligado, 5),
    ];

    private readonly OpcoesParadasAleatorias _opcoes;
    private readonly Random _aleatorio;
    private readonly Dictionary<int, DateTime> _proximaMudanca = new(); // por número da máquina

    /// <param name="aleatorio">Fixe a semente nos testes; nulo = sorteio de verdade.</param>
    public ParadasAleatorias(OpcoesParadasAleatorias opcoes, Random? aleatorio = null)
    {
        if (opcoes.RodandoMedio <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "O tempo médio rodando deve ser maior que zero.");
        if (opcoes.ParadaMinima <= TimeSpan.Zero || opcoes.ParadaMaxima < opcoes.ParadaMinima)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "A duração da parada deve ser maior que zero, com mínima até a máxima.");

        _opcoes = opcoes;
        _aleatorio = aleatorio ?? Random.Shared;
    }

    public OpcoesParadasAleatorias Opcoes => _opcoes;

    public bool Ligado(MaquinaSimulada maquina) => _proximaMudanca.ContainsKey(maquina.Numero);

    /// <summary>Quando a máquina muda de novo (para ou volta a rodar); nulo se não está no automático.</summary>
    public DateTime? ProximaMudanca(MaquinaSimulada maquina) =>
        _proximaMudanca.TryGetValue(maquina.Numero, out var quando) ? quando : null;

    /// <summary>Põe a máquina no automático a partir do cenário em que ela está.</summary>
    public void Ligar(MaquinaSimulada maquina, DateTime agoraUtc) =>
        _proximaMudanca[maquina.Numero] = agoraUtc + (maquina.Cenario == CenarioSimulado.Rodando ? TempoRodando() : TempoParada());

    /// <summary>Tira a máquina do automático (ela fica no cenário em que está).</summary>
    public void Desligar(MaquinaSimulada maquina) => _proximaMudanca.Remove(maquina.Numero);

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
        var opcoes = Pesos.Where(p => _opcoes.IncluirDesligado || p.Cenario != CenarioSimulado.Desligado).ToList();
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

    private TimeSpan TempoParada() =>
        _opcoes.ParadaMinima + TimeSpan.FromTicks((long)((_opcoes.ParadaMaxima - _opcoes.ParadaMinima).Ticks * _aleatorio.NextDouble()));
}
