using SmartLine.Core.Plc;

namespace SmartLine.Plc.Simulacao;

/// <summary>
/// Parâmetros do simulador contínuo.
///
/// Os defaults imitam uma paletizadora de 20 paletes/hora, com paradas
/// distribuídas de forma parecida com a realidade: falta de produto e acúmulo
/// são frequentes e curtos, falha de equipamento é rara e longa.
/// </summary>
public sealed record OpcoesSimuladorContinuo
{
    /// <summary>Ritmo de produção enquanto a máquina está operando.</summary>
    public double PaletesPorHora { get; init; } = 20;

    /// <summary>
    /// Tempo médio operando entre uma parada e a próxima. A duração real de
    /// cada período é sorteada em torno deste valor.
    /// </summary>
    public TimeSpan TempoMedioEntreParadas { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Semente do gerador aleatório.
    /// Fixa = comportamento reproduzível, para teste automatizado.
    /// <c>null</c> = varia a cada execução, para demonstração.
    /// </summary>
    public int? Semente { get; init; }

    /// <summary>
    /// Código do modo (WS_Cur_Mode_00100).
    /// Provisório: os valores reais da Network 26 ainda não foram levantados.
    /// </summary>
    public uint CodigoModo { get; init; }

    /// <summary>
    /// Código do programa (WS_Cur_Prog_00200).
    /// Provisório: os valores reais da Network 27 ainda não foram levantados.
    /// </summary>
    public uint CodigoPrograma { get; init; }

    /// <summary>Contador de paletes no início da simulação.</summary>
    public uint ContadorInicial { get; init; }

    /// <summary>Perfil de paradas. Peso maior = tipo mais frequente.</summary>
    public IReadOnlyList<ParadaSimulada> Paradas { get; init; } = PerfilPadrao;

    /// <summary>
    /// Velocidade de projeto em unidades/min, como o PLC reporta na
    /// WS_Mach_Design_Spd_00403. Derivada do ritmo para não ficarem
    /// inconsistentes entre si.
    /// </summary>
    public float VelocidadeProjetadaUnidadesPorMinuto => (float)(PaletesPorHora / 60.0);

    /// <summary>
    /// Perfil padrão de uma paletizadora.
    ///
    /// Os pesos somados dão 100 só por conveniência de leitura — o sorteio
    /// normaliza, então qualquer escala funciona.
    /// </summary>
    public static readonly IReadOnlyList<ParadaSimulada> PerfilPadrao =
    [
        // Frequentes e curtas — a linha em volta não acompanha. Paradas externas.
        new(EstadoWs.Lack,             Peso: 40, DuracaoMedia: TimeSpan.FromSeconds(90)),
        new(EstadoWs.Tailback,         Peso: 30, DuracaoMedia: TimeSpan.FromSeconds(120)),

        // Menos comum, duração média. Externa.
        new(EstadoWs.ExternalFailure,  Peso: 10, DuracaoMedia: TimeSpan.FromMinutes(5)),

        // Rara e longa — é a que derruba a Disponibilidade. Interna.
        new(EstadoWs.EquipmentFailure, Peso: 15, DuracaoMedia: TimeSpan.FromMinutes(15),
            CodigoFalha: 4201),

        // Rara e curta, mas interna.
        new(EstadoWs.EmergencyStop,    Peso: 5,  DuracaoMedia: TimeSpan.FromMinutes(3),
            CodigoFalha: 9001)
    ];

    /// <exception cref="ArgumentException">Algum parâmetro está inválido.</exception>
    public void Validar()
    {
        if (PaletesPorHora <= 0)
            throw new ArgumentException(
                $"Ritmo precisa ser positivo (recebido: {PaletesPorHora}).", nameof(PaletesPorHora));

        if (TempoMedioEntreParadas <= TimeSpan.Zero)
            throw new ArgumentException(
                "Tempo médio entre paradas precisa ser positivo.", nameof(TempoMedioEntreParadas));

        if (Paradas.Count == 0)
            throw new ArgumentException(
                "É preciso pelo menos um tipo de parada.", nameof(Paradas));

        if (Paradas.Any(p => p.Peso <= 0))
            throw new ArgumentException(
                "Todo tipo de parada precisa de peso positivo.", nameof(Paradas));

        if (Paradas.Any(p => p.DuracaoMedia <= TimeSpan.Zero))
            throw new ArgumentException(
                "Toda parada precisa de duração média positiva.", nameof(Paradas));

        if (Paradas.Any(p => p.Estado == EstadoWs.Operating))
            throw new ArgumentException(
                "Operating não é uma parada.", nameof(Paradas));
    }
}

/// <summary>Um tipo de parada no perfil do simulador.</summary>
/// <param name="Estado">Estado reportado enquanto a parada dura.</param>
/// <param name="Peso">Frequência relativa. Escala livre — o sorteio normaliza.</param>
/// <param name="DuracaoMedia">Duração média; cada ocorrência é sorteada em torno dela.</param>
/// <param name="CodigoFalha">
/// Código de alarme reportado junto (WS_Not_Of_Fail_Code_10000). Zero quando a
/// parada não gera alarme, como falta de produto.
/// </param>
public sealed record ParadaSimulada(
    EstadoWs Estado,
    double Peso,
    TimeSpan DuracaoMedia,
    uint CodigoFalha = 0);