namespace SmartLine.Core.Iot;

/// <summary>
/// Como uma entrada do WISE aparece para as pessoas: o nome e, nos sensores,
/// o texto de quando está ativo (em alarme) e de quando está normal. Ex.:
/// S8, "Falta de garrafas na entrada" / "Com garrafas na entrada".
/// </summary>
/// <param name="TextoAtivo">Só sensores de estado; nulo nos contadores.</param>
/// <param name="TextoNormal">Só sensores de estado; nulo nos contadores.</param>
public sealed record TextoEntrada(CanalWise Canal, string Nome, string? TextoAtivo, string? TextoNormal);

/// <summary>
/// Textos padrão das entradas. Cada máquina do catálogo pode personalizar os
/// seus; o que ela não personalizou fica com estes.
/// </summary>
/// <remarks>
/// Só texto: o que é alarme em cada sensor (a ligação invertida) continua
/// definido pelo <see cref="MapaWise"/> e não muda com a personalização.
/// </remarks>
public static class TextosEntradasWise
{
    public const int TamanhoMaximo = 60;

    public static IReadOnlyList<TextoEntrada> Padrao { get; } =
    [
        new(CanalWise.S1, "Acúmulo mínimo na entrada",         "Abaixo do acúmulo mínimo",          "Acúmulo mínimo normal"),
        new(CanalWise.S2, "Contador de produção (entrada 1)",  null,                                null),
        new(CanalWise.S3, "Contador de rejeito",               null,                                null),
        new(CanalWise.S4, "Acúmulo na saída (caixas/pallets)", "Saída de caixas/pallets bloqueada", "Saída de caixas/pallets livre"),
        new(CanalWise.S5, "Contador de produção (entrada 2)",  null,                                null),
        new(CanalWise.S6, "Contador de produção (entrada 3)",  null,                                null),
        new(CanalWise.S7, "Acúmulo na saída de garrafas",      "Saída de garrafas bloqueada",       "Saída de garrafas livre"),
        new(CanalWise.S8, "Falta de garrafas na entrada",      "Falta de garrafas na entrada",      "Com garrafas na entrada"),
    ];

    public static TextoEntrada DoPadrao(CanalWise canal) =>
        Enum.IsDefined(canal)
            ? Padrao[(int)canal - 1]
            : throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal inexistente no WISE-4051.");
}
