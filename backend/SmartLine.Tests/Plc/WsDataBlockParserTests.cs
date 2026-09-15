using System.Buffers.Binary;
using SmartLine.Core.Plc;
using SmartLine.Plc.Parsing;

namespace SmartLine.Tests.Plc;

/// <summary>
/// Testes do <see cref="WsDataBlockParser"/>.
///
/// Rodam sem PLC, sem rede e sem banco: o parser é função pura, então todo
/// cenário é um byte[88] montado na mão.
/// </summary>
public class WsDataBlockParserTests
{
    private static readonly DateTime Agora =
        new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------
    // Round-trip
    // ---------------------------------------------------------------

    /// <summary>
    /// Cada campo recebe um valor único derivado do próprio offset
    /// (UDInt = 1000 + offset, Real = offset + 0,5).
    /// Se dois offsets estiverem trocados na tabela, os dois valores saem
    /// trocados e o teste aponta exatamente quais.
    /// </summary>
    [Fact]
    public void Parse_ComTodosOsCampos_LeCadaUmDoOffsetCerto()
    {
        var buffer = NovoBuffer();

        // Status
        EscreverUDInt(buffer, 0, 1000);
        EscreverUDInt(buffer, 4, 1004);
        EscreverUDInt(buffer, 8, 1008);
        EscreverReal(buffer, 12, 12.5f);
        EscreverReal(buffer, 16, 16.5f);
        EscreverReal(buffer, 20, 20.5f);
        EscreverUDInt(buffer, 24, 1024);
        EscreverUDInt(buffer, 28, 1028);

        // Produtos
        EscreverUDInt(buffer, 32, 1032);
        EscreverUDInt(buffer, 36, 1036);
        EscreverUDInt(buffer, 40, 1040);
        EscreverUDInt(buffer, 44, 1044);
        EscreverUDInt(buffer, 48, 1048);

        // Contadores
        EscreverUDInt(buffer, 52, 1052);
        EscreverUDInt(buffer, 56, 1056);
        EscreverUDInt(buffer, 60, 1060);
        EscreverUDInt(buffer, 64, 1064);

        // Sanmartin
        EscreverUDInt(buffer, 68, 1068);
        EscreverReal(buffer, 72, 72.5f);
        EscreverReal(buffer, 76, 76.5f);
        EscreverReal(buffer, 80, 80.5f);
        EscreverUDInt(buffer, 84, 1084);

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(Agora, leitura.TimestampUtc);

        Assert.Equal(1000u, leitura.CodigoModo);
        Assert.Equal(1004u, leitura.CodigoPrograma);
        Assert.Equal(1008u, leitura.CodigoEstado);
        Assert.Equal(12.5f, leitura.VelocidadeAtual);
        Assert.Equal(16.5f, leitura.VelocidadeSetada);
        Assert.Equal(20.5f, leitura.VelocidadeProjetada);
        Assert.Equal(1024u, leitura.RazaoProducao);
        Assert.Equal(1028u, leitura.CodigoFalha);

        Assert.Equal(1032u, leitura.TipoPalete);
        Assert.Equal(1036u, leitura.TipoCaixa);
        Assert.Equal(1040u, leitura.TipoGarrafa);
        Assert.Equal(1044u, leitura.TipoBebida);
        Assert.Equal(1048u, leitura.TipoPacote);

        Assert.Equal(1052u, leitura.TotalPaletes);
        Assert.Equal(1056u, leitura.TotalCaixas);
        Assert.Equal(1060u, leitura.TotalGarrafas);
        Assert.Equal(1064u, leitura.TotalPacotes);

        Assert.Equal(1068u, leitura.NumeroReceita);
        Assert.Equal(72.5f, leitura.TotalHectolitros);
        Assert.Equal(76.5f, leitura.ConsumoEnergia);
        Assert.Equal(80.5f, leitura.HorasOperacao);
        Assert.Equal(1084u, leitura.TipoProduto);
    }

    [Fact]
    public void Parse_BufferZerado_NaoExplodeERetornaTudoZero()
    {
        var leitura = WsDataBlockParser.Parse(NovoBuffer(), Agora);

        Assert.Equal(0u, leitura.CodigoEstado);
        Assert.Equal(0u, leitura.TotalPaletes);
        Assert.Equal(0f, leitura.VelocidadeAtual);
    }

    // ---------------------------------------------------------------
    // Endianness — o erro que não dá exceção
    // ---------------------------------------------------------------

    /// <summary>
    /// Bytes 00 00 00 01 em big-endian valem 1. Lendo little-endian
    /// virariam 16.777.216 — sem exceção nenhuma, só um número absurdo
    /// envenenando o OEE. Este é o teste mais importante do arquivo.
    /// </summary>
    [Fact]
    public void Parse_UDInt_UsaBigEndian()
    {
        var buffer = NovoBuffer();
        buffer[WsDataBlockLayout.Offsets.TotalPaletes + 0] = 0x00;
        buffer[WsDataBlockLayout.Offsets.TotalPaletes + 1] = 0x00;
        buffer[WsDataBlockLayout.Offsets.TotalPaletes + 2] = 0x00;
        buffer[WsDataBlockLayout.Offsets.TotalPaletes + 3] = 0x01;

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(1u, leitura.TotalPaletes);
        Assert.NotEqual(16_777_216u, leitura.TotalPaletes);
    }

    /// <summary>Real 1.0f em IEEE-754 big-endian é 3F 80 00 00.</summary>
    [Fact]
    public void Parse_Real_UsaBigEndian()
    {
        var buffer = NovoBuffer();
        buffer[WsDataBlockLayout.Offsets.VelocidadeAtual + 0] = 0x3F;
        buffer[WsDataBlockLayout.Offsets.VelocidadeAtual + 1] = 0x80;
        buffer[WsDataBlockLayout.Offsets.VelocidadeAtual + 2] = 0x00;
        buffer[WsDataBlockLayout.Offsets.VelocidadeAtual + 3] = 0x00;

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(1.0f, leitura.VelocidadeAtual);
    }

    // ---------------------------------------------------------------
    // Limites do contador
    // ---------------------------------------------------------------

    /// <summary>
    /// UDInt no topo da faixa. Se o parser usasse int, sairia -1 e o
    /// tratamento de rollover trataria como erro em vez de virada.
    /// </summary>
    [Fact]
    public void Parse_ContadorNoMaximo_NaoViraNegativo()
    {
        var buffer = NovoBuffer();
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalPaletes, uint.MaxValue);

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(4_294_967_295u, leitura.TotalPaletes);
    }

    /// <summary>Acima de 2.147.483.647 é onde int quebraria silenciosamente.</summary>
    [Fact]
    public void Parse_ContadorAcimaDoLimiteDeInt_LeCorretamente()
    {
        var buffer = NovoBuffer();
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalGarrafas, 3_000_000_000);

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(3_000_000_000u, leitura.TotalGarrafas);
    }

    // ---------------------------------------------------------------
    // Validação de entrada
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(87)]
    public void Parse_BufferMenorQueODb_Explode(int tamanho)
    {
        var buffer = new byte[tamanho];

        var erro = Assert.Throws<ArgumentException>(
            () => WsDataBlockParser.Parse(buffer, Agora));

        Assert.Equal("buffer", erro.ParamName);
    }

    /// <summary>Bytes a mais são inofensivos: o parser lê só os 88 primeiros.</summary>
    [Fact]
    public void Parse_BufferMaiorQueODb_IgnoraOExcedente()
    {
        var buffer = new byte[WsDataBlockLayout.TamanhoBytes + 16];
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalPaletes, 42);

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(42u, leitura.TotalPaletes);
    }

    /// <summary>
    /// Hora local aqui desloca o gráfico por hora e a virada da meia-noite.
    /// Melhor explodir na coleta do que descobrir no relatório.
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Parse_TimestampForaDeUtc_Explode(DateTimeKind kind)
    {
        var buffer = NovoBuffer();
        var timestamp = new DateTime(2026, 9, 15, 12, 0, 0, kind);

        var erro = Assert.Throws<ArgumentException>(
            () => WsDataBlockParser.Parse(buffer, timestamp));

        Assert.Equal("timestampUtc", erro.ParamName);
    }

    // ---------------------------------------------------------------
    // Seleção do contador de produção
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(50001, 11u)]
    [InlineData(50002, 22u)]
    [InlineData(50005, 33u)]
    [InlineData(50220, 44u)]
    public void ContadorPorTag_DevolveOContadorCerto(ushort tagId, uint esperado)
    {
        var buffer = NovoBuffer();
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalPaletes, 11);
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalCaixas, 22);
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalGarrafas, 33);
        EscreverUDInt(buffer, WsDataBlockLayout.Offsets.TotalPacotes, 44);

        var leitura = WsDataBlockParser.Parse(buffer, Agora);

        Assert.Equal(esperado, leitura.ContadorPorTag(tagId));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)300)]
    [InlineData((ushort)50003)]
    public void ContadorPorTag_ComTagQueNaoEContador_Explode(ushort tagId)
    {
        var leitura = WsDataBlockParser.Parse(NovoBuffer(), Agora);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => leitura.ContadorPorTag(tagId));
    }

    // ---------------------------------------------------------------
    // Coerência da tabela de layout
    // ---------------------------------------------------------------
    // Estes testes não exercitam o parser: validam a própria tabela.
    // É onde um erro de digitação passaria despercebido.

    [Fact]
    public void Layout_TemOs22PontosDoDb()
    {
        Assert.Equal(22, WsDataBlockLayout.Pontos.Count);
    }

    [Fact]
    public void Layout_OffsetsSaoContiguosDeQuatroEmQuatro()
    {
        var offsets = WsDataBlockLayout.Pontos
            .Select(p => p.Offset)
            .OrderBy(x => x)
            .ToArray();

        var esperado = Enumerable.Range(0, 22).Select(i => i * 4).ToArray();

        Assert.Equal(esperado, offsets);
    }

    [Fact]
    public void Layout_UltimoPontoFechaEmOitentaEOito()
    {
        var fim = WsDataBlockLayout.Pontos.Max(p => p.Offset + p.Tamanho);

        Assert.Equal(WsDataBlockLayout.TamanhoBytes, fim);
    }

    [Fact]
    public void Layout_NaoTemTagIdRepetido()
    {
        var tags = WsDataBlockLayout.Pontos.Select(p => p.TagId).ToArray();

        Assert.Equal(tags.Length, tags.Distinct().Count());
    }

    /// <summary>
    /// O nome no PLC termina com o TagId em 5 dígitos
    /// (WS_Total_Pallet_50001 → 50001). Se a transcrição do DB errou um
    /// dígito, este teste pega.
    /// </summary>
    [Fact]
    public void Layout_NomeDoPlcTerminaComOTagId()
    {
        foreach (var ponto in WsDataBlockLayout.Pontos)
        {
            var sufixo = ponto.NomePlc[^5..];

            Assert.True(
                ushort.TryParse(sufixo, out var tagDoNome),
                $"'{ponto.NomePlc}' não termina em 5 dígitos.");

            Assert.True(
                tagDoNome == ponto.TagId,
                $"'{ponto.NomePlc}' termina em {tagDoNome}, mas está cadastrado como {ponto.TagId}.");
        }
    }

    [Fact]
    public void Layout_TodasAsTagsDeContadorExistemNaTabela()
    {
        foreach (var tagId in WsDataBlockLayout.TagsDeContador)
        {
            Assert.NotNull(WsDataBlockLayout.BuscarPorTag(tagId));
        }
    }

    [Fact]
    public void BuscarPorTag_ComTagInexistente_RetornaNull()
    {
        Assert.Null(WsDataBlockLayout.BuscarPorTag(60000));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static byte[] NovoBuffer() => new byte[WsDataBlockLayout.TamanhoBytes];

    private static void EscreverUDInt(byte[] buffer, int offset, uint valor) =>
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), valor);

    private static void EscreverReal(byte[] buffer, int offset, float valor) =>
        BinaryPrimitives.WriteSingleBigEndian(buffer.AsSpan(offset, 4), valor);
}
