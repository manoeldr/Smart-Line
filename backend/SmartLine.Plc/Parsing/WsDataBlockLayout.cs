namespace SmartLine.Plc.Parsing;

/// <summary>
/// Mapa do DB Weihenstephan da máquina (bloco de acesso padrão, não otimizado).
/// Offsets e tipos conferidos contra o export do TIA Portal.
///
/// Convenção de nome no PLC: WS_&lt;Nome&gt;_&lt;TagId com 5 dígitos&gt;.
/// O sufixo numérico é o TagId do padrão Weihenstephan; o prefixo SNM_ marca
/// os pontos proprietários da Sanmartin.
/// </summary>
public static class WsDataBlockLayout
{
    /// <summary>Tamanho total do DB, em bytes. Toda leitura S7 deve trazer exatamente isto.</summary>
    public const int TamanhoBytes = 88;

    /// <summary>
    /// TagIds do padrão. Todos cabem em 16 bits (o maior é 50220).
    /// </summary>
    public static class Tags
    {
        // Status
        public const ushort ModoAtual = 100;
        public const ushort ProgramaAtual = 200;
        public const ushort EstadoAtual = 300;
        public const ushort VelocidadeAtual = 401;
        public const ushort VelocidadeSetada = 402;
        public const ushort VelocidadeProjetada = 403;
        public const ushort RazaoProducao = 701;
        public const ushort CodigoFalha = 10000;

        // Produtos
        public const ushort TipoPalete = 30001;
        public const ushort TipoCaixa = 30002;
        public const ushort TipoGarrafa = 30003;
        public const ushort TipoBebida = 30004;
        public const ushort TipoPacote = 30100;

        // Contadores
        public const ushort TotalPaletes = 50001;
        public const ushort TotalCaixas = 50002;
        public const ushort TotalGarrafas = 50005;
        public const ushort TotalPacotes = 50220;

        // Sanmartin (proprietário)
        public const ushort NumeroReceita = 5000;
        public const ushort TotalHectolitros = 5001;
        public const ushort ConsumoEnergia = 5002;
        public const ushort HorasOperacao = 5003;
        public const ushort TipoProduto = 5004;
    }

    /// <summary>
    /// Offsets em bytes a partir do início do DB.
    /// Todo dado ocupa 4 bytes (UDInt ou Real), sem padding entre as structs.
    /// </summary>
    public static class Offsets
    {
        // StatusVariables — struct em 0.0
        public const int ModoAtual = 0;
        public const int ProgramaAtual = 4;
        public const int EstadoAtual = 8;
        public const int VelocidadeAtual = 12;
        public const int VelocidadeSetada = 16;
        public const int VelocidadeProjetada = 20;
        public const int RazaoProducao = 24;
        public const int CodigoFalha = 28;

        // ProductsVariables — struct em 32.0
        public const int TipoPalete = 32;
        public const int TipoCaixa = 36;
        public const int TipoGarrafa = 40;
        public const int TipoBebida = 44;
        public const int TipoPacote = 48;

        // CountersVariables — struct em 52.0
        public const int TotalPaletes = 52;
        public const int TotalCaixas = 56;
        public const int TotalGarrafas = 60;
        public const int TotalPacotes = 64;

        // SanmartinVariables — struct em 68.0
        public const int NumeroReceita = 68;
        public const int TotalHectolitros = 72;
        public const int ConsumoEnergia = 76;
        public const int HorasOperacao = 80;
        public const int TipoProduto = 84;
    }

    /// <summary>Tipo de dado de um ponto do DB.</summary>
    public enum TipoDado
    {
        /// <summary>UDInt — inteiro sem sinal de 32 bits.</summary>
        UDInt,

        /// <summary>Real — ponto flutuante IEEE-754 de 32 bits.</summary>
        Real
    }

    /// <summary>Descrição de um ponto do DB.</summary>
    /// <param name="TagId">TagId Weihenstephan.</param>
    /// <param name="NomePlc">Nome exato da variável no PLC (rastreabilidade).</param>
    /// <param name="Offset">Offset em bytes dentro do DB.</param>
    /// <param name="Tipo">Tipo de dado.</param>
    public sealed record Ponto(ushort TagId, string NomePlc, int Offset, TipoDado Tipo)
    {
        /// <summary>Tamanho em bytes. Todos os tipos usados aqui ocupam 4.</summary>
        public int Tamanho => 4;
    }

    /// <summary>Todos os pontos do DB, na ordem física.</summary>
    public static readonly IReadOnlyList<Ponto> Pontos =
    [
        new(Tags.ModoAtual,            "WS_Cur_Mode_00100",         Offsets.ModoAtual,            TipoDado.UDInt),
        new(Tags.ProgramaAtual,        "WS_Cur_Prog_00200",         Offsets.ProgramaAtual,        TipoDado.UDInt),
        new(Tags.EstadoAtual,          "WS_Cur_State_00300",        Offsets.EstadoAtual,          TipoDado.UDInt),
        new(Tags.VelocidadeAtual,      "WS_Cur_Mach_Spd_00401",     Offsets.VelocidadeAtual,      TipoDado.Real),
        new(Tags.VelocidadeSetada,     "WS_Set_Mach_Spd_00402",     Offsets.VelocidadeSetada,     TipoDado.Real),
        new(Tags.VelocidadeProjetada,  "WS_Mach_Design_Spd_00403",  Offsets.VelocidadeProjetada,  TipoDado.Real),
        new(Tags.RazaoProducao,        "WS_Prod_Ratio_00701",       Offsets.RazaoProducao,        TipoDado.UDInt),
        new(Tags.CodigoFalha,          "WS_Not_Of_Fail_Code_10000", Offsets.CodigoFalha,          TipoDado.UDInt),

        new(Tags.TipoPalete,           "WS_Pallet_Type_30001",      Offsets.TipoPalete,           TipoDado.UDInt),
        new(Tags.TipoCaixa,            "WS_Crate_Type_30002",       Offsets.TipoCaixa,            TipoDado.UDInt),
        new(Tags.TipoGarrafa,          "WS_Bottle_Type_30003",      Offsets.TipoGarrafa,          TipoDado.UDInt),
        new(Tags.TipoBebida,           "WS_Beer_Type_30004",        Offsets.TipoBebida,           TipoDado.UDInt),
        new(Tags.TipoPacote,           "WS_Package_Type_30100",     Offsets.TipoPacote,           TipoDado.UDInt),

        new(Tags.TotalPaletes,         "WS_Total_Pallet_50001",     Offsets.TotalPaletes,         TipoDado.UDInt),
        new(Tags.TotalCaixas,          "WS_Total_Crates_50002",     Offsets.TotalCaixas,          TipoDado.UDInt),
        new(Tags.TotalGarrafas,        "WS_Total_Bottles_50005",    Offsets.TotalGarrafas,        TipoDado.UDInt),
        new(Tags.TotalPacotes,         "WS_Total_Packages_50220",   Offsets.TotalPacotes,         TipoDado.UDInt),

        new(Tags.NumeroReceita,        "SNM_Num_Recipe_05000",      Offsets.NumeroReceita,        TipoDado.UDInt),
        new(Tags.TotalHectolitros,     "SNM_Tot_Hectos_05001",      Offsets.TotalHectolitros,     TipoDado.Real),
        new(Tags.ConsumoEnergia,       "SNM_Cons_Elec_Energy_05002",Offsets.ConsumoEnergia,       TipoDado.Real),
        new(Tags.HorasOperacao,        "SNM_Operating_Hours_05003", Offsets.HorasOperacao,        TipoDado.Real),
        new(Tags.TipoProduto,          "SNM_Prod_Type_05004",       Offsets.TipoProduto,          TipoDado.UDInt)
    ];

    /// <summary>
    /// TagIds que podem ser usados como contador de produção.
    /// Qual deles vale para cada máquina é configuração, não regra fixa:
    /// numa paletizadora é <see cref="Tags.TotalPaletes"/>, numa enchedora
    /// costuma ser <see cref="Tags.TotalGarrafas"/>.
    /// </summary>
    public static readonly IReadOnlySet<ushort> TagsDeContador = new HashSet<ushort>
    {
        Tags.TotalPaletes,
        Tags.TotalCaixas,
        Tags.TotalGarrafas,
        Tags.TotalPacotes
    };

    /// <summary>Busca um ponto pelo TagId. Devolve null se o TagId não existir neste DB.</summary>
    public static Ponto? BuscarPorTag(ushort tagId) =>
        Pontos.FirstOrDefault(p => p.TagId == tagId);
}