namespace SmartLine.Core.Iot;

/// <summary>
/// Uma mensagem do WISE já interpretada: o que ele informou, sem nenhuma
/// regra aplicada. Contrato entre o parser MQTT (que depende do formato do
/// firmware) e o <see cref="EstadoMaquinaIot"/> (que não depende).
/// </summary>
/// <remarks>
/// Uma mensagem pode trazer só parte dos canais (ex.: um C.O.S. de sensor sem
/// os contadores). Canal ausente = sem novidade, mantém o último valor.
/// </remarks>
public sealed record AmostraWise
{
    /// <summary>Instante da amostra, em UTC (hoje: hora de chegada no backend).</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>Valor acumulado de cada contador informado.</summary>
    public IReadOnlyDictionary<CanalWise, uint> Contadores { get; init; } = new Dictionary<CanalWise, uint>();

    /// <summary>Valor bruto de cada sensor de estado informado (true = 1).</summary>
    public IReadOnlyDictionary<CanalWise, bool> Estados { get; init; } = new Dictionary<CanalWise, bool>();
}
