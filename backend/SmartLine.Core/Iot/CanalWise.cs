namespace SmartLine.Core.Iot;

/// <summary>
/// Canais do WISE-4051. <c>Sn</c> corresponde à entrada física <c>I(n-1)</c>.
/// </summary>
public enum CanalWise
{
    S1 = 1,
    S2 = 2,
    S3 = 3,
    S4 = 4,
    S5 = 5,
    S6 = 6,
    S7 = 7,
    S8 = 8
}

/// <summary>Como o canal é lido.</summary>
public enum TipoCanal
{
    /// <summary>Conta pulsos (filtro de 2 ms no WISE, publicação periódica).</summary>
    Contador,

    /// <summary>Presença de produto (filtro de 10 s e C.O.S. no WISE).</summary>
    Estado
}

/// <summary>O que o sensor ligado ao canal representa na máquina.</summary>
public enum FuncaoCanal
{
    AcumuloMinimoEntrada,
    ContadorProducao,
    ContadorRejeito,
    AcumuloSaida,
    AcumuloSaidaGarrafas,
    FaltaGarrafasEntrada
}

/// <summary>Definição fixa de um canal.</summary>
/// <param name="NivelAlarme">
/// Valor bruto que indica problema, só para canais de estado. Os sensores de
/// estado são ligados invertidos (1 = sem presença, 0 = presença), então
/// "falta na entrada" alarma em 1 e "acúmulo na saída" alarma em 0.
/// </param>
public sealed record DefinicaoCanal(
    CanalWise Canal,
    TipoCanal Tipo,
    FuncaoCanal Funcao,
    string Descricao,
    bool? NivelAlarme)
{
    /// <summary>Índice da entrada física no WISE (I0 a I7).</summary>
    public int Entrada => (int)Canal - 1;

    /// <summary>Canal de contador de produção (S2, S5 ou S6).</summary>
    public bool EhContadorProducao => Funcao == FuncaoCanal.ContadorProducao;

    /// <summary>Traduz o valor bruto do sensor para "está em alarme".</summary>
    /// <exception cref="InvalidOperationException">Canal é contador, não tem alarme.</exception>
    public bool EstaEmAlarme(bool valorBruto) => NivelAlarme is { } nivel
        ? valorBruto == nivel
        : throw new InvalidOperationException($"{Canal} é contador e não tem nível de alarme.");
}

/// <summary>
/// Mapa de entradas, igual em todos os WISE instalados. Por isso é constante
/// no código e não cadastro: trocar a ligação de um sensor exige trocar em
/// todos os dispositivos, o que é uma mudança de versão, não de configuração.
/// </summary>
public static class MapaWise
{
    /// <summary>Os 8 canais, em ordem (índice 0 = S1).</summary>
    public static IReadOnlyList<DefinicaoCanal> Canais { get; } =
    [
        new(CanalWise.S1, TipoCanal.Estado,   FuncaoCanal.AcumuloMinimoEntrada, "Acúmulo mínimo na entrada",           NivelAlarme: true),
        new(CanalWise.S2, TipoCanal.Contador, FuncaoCanal.ContadorProducao,     "Contador de produção (entrada 1)",    NivelAlarme: null),
        new(CanalWise.S3, TipoCanal.Contador, FuncaoCanal.ContadorRejeito,      "Contador de rejeito",                 NivelAlarme: null),
        new(CanalWise.S4, TipoCanal.Estado,   FuncaoCanal.AcumuloSaida,         "Acúmulo na saída (caixas/pallets)",   NivelAlarme: false),
        new(CanalWise.S5, TipoCanal.Contador, FuncaoCanal.ContadorProducao,     "Contador de produção (entrada 2)",    NivelAlarme: null),
        new(CanalWise.S6, TipoCanal.Contador, FuncaoCanal.ContadorProducao,     "Contador de produção (entrada 3)",    NivelAlarme: null),
        new(CanalWise.S7, TipoCanal.Estado,   FuncaoCanal.AcumuloSaidaGarrafas, "Acúmulo na saída de garrafas",        NivelAlarme: false),
        new(CanalWise.S8, TipoCanal.Estado,   FuncaoCanal.FaltaGarrafasEntrada, "Falta de garrafas na entrada",        NivelAlarme: true),
    ];

    /// <summary>Definição de um canal.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Valor fora de S1..S8.</exception>
    public static DefinicaoCanal Definicao(CanalWise canal) =>
        Enum.IsDefined(canal)
            ? Canais[(int)canal - 1]
            : throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal inexistente no WISE-4051.");
}
