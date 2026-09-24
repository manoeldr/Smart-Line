using SmartLine.Core.Enums;
using SmartLine.Core.Iot;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Blocos para montar cenários de teste do IoT em termos da fábrica, sem
/// pensar na polaridade invertida dos sensores.
/// </summary>
internal static class Cenario
{
    public static readonly DateTime T0 = new(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc);

    public static readonly Guid MotivoFaltaGarrafas = Guid.Parse("00000000-0000-0000-0000-0000000000a8");
    public static readonly Guid MotivoAcumuloMinimo = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid MotivoSaidaGarrafas = Guid.Parse("00000000-0000-0000-0000-0000000000a7");
    public static readonly Guid MotivoSaidaCaixas = Guid.Parse("00000000-0000-0000-0000-0000000000a4");

    /// <summary>As regras padrão da especificação.</summary>
    public static IReadOnlyList<Regra> RegrasPadrao { get; } =
    [
        new(Guid.NewGuid(), 1, [Condicao.EmAlarme(CanalWise.S8)], TipoParada.Externa, MotivoFaltaGarrafas),
        new(Guid.NewGuid(), 2, [Condicao.EmAlarme(CanalWise.S1)], TipoParada.Externa, MotivoAcumuloMinimo),
        new(Guid.NewGuid(), 3, [Condicao.EmAlarme(CanalWise.S7)], TipoParada.Externa, MotivoSaidaGarrafas),
        new(Guid.NewGuid(), 4, [Condicao.EmAlarme(CanalWise.S4)], TipoParada.Externa, MotivoSaidaCaixas),
    ];

    public static readonly TimeSpan Z = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LimiteComunicacao = TimeSpan.FromSeconds(90);

    /// <summary>Todos os sensores de estado + S2 (×1) + S3 (×1), salvo indicação.</summary>
    public static ConfiguracaoColetaIot Config(IEnumerable<CanalMedicao>? canais = null, IEnumerable<Regra>? regras = null) =>
        new(
            canais ??
            [
                new(CanalWise.S1), new(CanalWise.S2), new(CanalWise.S3), new(CanalWise.S4),
                new(CanalWise.S7), new(CanalWise.S8)
            ],
            Z,
            LimiteComunicacao,
            regras ?? RegrasPadrao);

    public static DateTime Em(int segundos) => T0.AddSeconds(segundos);

    /// <summary>
    /// Amostra com os sensores descritos em termos lógicos (todos normais por
    /// padrão) e os contadores informados.
    /// </summary>
    public static AmostraWise Amostra(
        int segundos,
        uint? s2 = null,
        uint? s3 = null,
        uint? s5 = null,
        uint? s6 = null,
        bool faltaGarrafas = false,
        bool abaixoAcumuloMinimo = false,
        bool saidaCaixasBloqueada = false,
        bool saidaGarrafasBloqueada = false,
        bool semSensores = false)
    {
        var contadores = new Dictionary<CanalWise, uint>();
        if (s2 is { } v2) contadores[CanalWise.S2] = v2;
        if (s3 is { } v3) contadores[CanalWise.S3] = v3;
        if (s5 is { } v5) contadores[CanalWise.S5] = v5;
        if (s6 is { } v6) contadores[CanalWise.S6] = v6;

        var estados = new Dictionary<CanalWise, bool>();
        if (!semSensores)
        {
            // Invertidos: 1 = sem presença, 0 = presença.
            estados[CanalWise.S1] = abaixoAcumuloMinimo;       // alarma em 1
            estados[CanalWise.S8] = faltaGarrafas;             // alarma em 1
            estados[CanalWise.S4] = !saidaCaixasBloqueada;     // alarma em 0
            estados[CanalWise.S7] = !saidaGarrafasBloqueada;   // alarma em 0
        }

        return new AmostraWise { TimestampUtc = Em(segundos), Contadores = contadores, Estados = estados };
    }
}
