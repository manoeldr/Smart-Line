namespace SmartLine.Core.Plc;

/// <summary>
/// Retrato de uma máquina num instante, lido do PLC.
///
/// É o único contrato entre a camada de coleta e o domínio: o coletor, o cálculo
/// de OEE e a virada de sessão trabalham só com isto, sem saber se veio por S7,
/// OPC UA, WS Protocol ou simulador.
///
/// Modo, Programa e Estado ficam como código bruto de propósito. A tradução para
/// <see cref="EstadoWs"/> é configurável por máquina, porque os valores numéricos
/// variam entre implementações do padrão.
/// </summary>
public sealed record LeituraPlc
{
    /// <summary>Instante da leitura, sempre em UTC (ver ValueConverter global do EF).</summary>
    public required DateTime TimestampUtc { get; init; }

    // ---- Status ----

    /// <summary>WS_Cur_Mode_00100 — modo atual (Automático / Semi / Manual), código bruto.</summary>
    public required uint CodigoModo { get; init; }

    /// <summary>WS_Cur_Prog_00200 — programa atual (Produção / Start-Up / Manutenção), código bruto.</summary>
    public required uint CodigoPrograma { get; init; }

    /// <summary>WS_Cur_State_00300 — estado atual (Operating, Lack, Tailback...), código bruto.</summary>
    public required uint CodigoEstado { get; init; }

    /// <summary>WS_Cur_Mach_Spd_00401 — velocidade atual.</summary>
    public required float VelocidadeAtual { get; init; }

    /// <summary>
    /// WS_Set_Mach_Spd_00402 — velocidade nominal setada.
    /// Atenção: marcada como não usada na paletizadora; pode vir zerada.
    /// </summary>
    public required float VelocidadeSetada { get; init; }

    /// <summary>WS_Mach_Design_Spd_00403 — velocidade de projeto, em unidades/min.</summary>
    public required float VelocidadeProjetada { get; init; }

    /// <summary>WS_Prod_Ratio_00701 — produtos primários por embalagem secundária.</summary>
    public required uint RazaoProducao { get; init; }

    /// <summary>
    /// WS_Not_Of_Fail_Code_10000 — código da PRIMEIRA falha ativa.
    /// Não é a lista completa: se houver vários alarmes, só um aparece aqui.
    /// </summary>
    public required uint CodigoFalha { get; init; }

    // ---- Produtos ----

    /// <summary>WS_Pallet_Type_30001.</summary>
    public required uint TipoPalete { get; init; }

    /// <summary>WS_Crate_Type_30002 — não usado na paletizadora.</summary>
    public required uint TipoCaixa { get; init; }

    /// <summary>WS_Bottle_Type_30003.</summary>
    public required uint TipoGarrafa { get; init; }

    /// <summary>WS_Beer_Type_30004 — não usado na paletizadora.</summary>
    public required uint TipoBebida { get; init; }

    /// <summary>WS_Package_Type_30100 — não usado na paletizadora.</summary>
    public required uint TipoPacote { get; init; }

    // ---- Contadores (acumulados, podem dar rollover) ----

    /// <summary>WS_Total_Pallet_50001.</summary>
    public required uint TotalPaletes { get; init; }

    /// <summary>WS_Total_Crates_50002.</summary>
    public required uint TotalCaixas { get; init; }

    /// <summary>WS_Total_Bottles_50005.</summary>
    public required uint TotalGarrafas { get; init; }

    /// <summary>WS_Total_Packages_50220.</summary>
    public required uint TotalPacotes { get; init; }

    // ---- Sanmartin ----

    /// <summary>SNM_Num_Recipe_05000.</summary>
    public required uint NumeroReceita { get; init; }

    /// <summary>
    /// SNM_Tot_Hectos_05001 — totalizador de hectolitros.
    /// É Real de 32 bits (~7 dígitos significativos): acima de alguns milhões,
    /// incrementos pequenos somem. Use diferença entre leituras próximas,
    /// nunca confie no acumulado absoluto.
    /// </summary>
    public required float TotalHectolitros { get; init; }

    /// <summary>SNM_Cons_Elec_Energy_05002 — mesma ressalva de precisão do totalizador.</summary>
    public required float ConsumoEnergia { get; init; }

    /// <summary>
    /// SNM_Operating_Hours_05003 — horas de operação acumuladas.
    /// Fonte independente para validar o tempo rodando calculado pelo SmartLine.
    /// </summary>
    public required float HorasOperacao { get; init; }

    /// <summary>SNM_Prod_Type_05004.</summary>
    public required uint TipoProduto { get; init; }

    /// <summary>
    /// Leitura com todos os valores zerados, para ser refinada com <c>with</c>.
    ///
    /// As 23 propriedades são <c>required</c>, então sem isto qualquer leitura
    /// montada na mão precisaria preencher todas — inclusive as irrelevantes
    /// para o caso. Usada pelo simulador e pelos testes:
    ///
    /// <code>
    /// LeituraPlc.Zerada(agora) with { CodigoEstado = 5, TotalPaletes = 42 }
    /// </code>
    /// </summary>
    /// <param name="timestampUtc">Instante da leitura, em UTC.</param>
    /// <exception cref="ArgumentException">Timestamp não está em UTC.</exception>
    public static LeituraPlc Zerada(DateTime timestampUtc)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Timestamp precisa estar em UTC.", nameof(timestampUtc));
        }

        return new LeituraPlc
        {
            TimestampUtc = timestampUtc,
            CodigoModo = 0,
            CodigoPrograma = 0,
            CodigoEstado = 0,
            VelocidadeAtual = 0,
            VelocidadeSetada = 0,
            VelocidadeProjetada = 0,
            RazaoProducao = 0,
            CodigoFalha = 0,
            TipoPalete = 0,
            TipoCaixa = 0,
            TipoGarrafa = 0,
            TipoBebida = 0,
            TipoPacote = 0,
            TotalPaletes = 0,
            TotalCaixas = 0,
            TotalGarrafas = 0,
            TotalPacotes = 0,
            NumeroReceita = 0,
            TotalHectolitros = 0,
            ConsumoEnergia = 0,
            HorasOperacao = 0,
            TipoProduto = 0
        };
    }

    /// <summary>
    /// Devolve o contador correspondente ao TagId configurado como produção da máquina.
    /// </summary>
    /// <param name="tagIdContador">50001, 50002, 50005 ou 50220.</param>
    /// <exception cref="ArgumentOutOfRangeException">TagId não é um contador válido.</exception>
    public uint ContadorPorTag(ushort tagIdContador) => tagIdContador switch
    {
        50001 => TotalPaletes,
        50002 => TotalCaixas,
        50005 => TotalGarrafas,
        50220 => TotalPacotes,
        _ => throw new ArgumentOutOfRangeException(
            nameof(tagIdContador),
            tagIdContador,
            "TagId não corresponde a nenhum contador de produção do DB.")
    };
}