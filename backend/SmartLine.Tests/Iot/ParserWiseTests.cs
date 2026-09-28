using System.Text;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Wise;

namespace SmartLine.Tests.Iot;

public class ParserWiseTests
{
    private static readonly DateTime Chegada = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static ResultadoWise Interpretar(string payload, string topico = "Advantech/00D0C9AABBCC/data") =>
        ParserWise.Interpretar(new MensagemMqtt("WISE-ENCH", topico, Encoding.UTF8.GetBytes(payload), Chegada));

    // ── Tópico ──────────────────────────────────────────────────────

    [Fact]
    public void TopicoDeDados_IdentificaPeloMac_EmMaiusculas()
    {
        var r = Interpretar("{}", "Advantech/00d0c9aabbcc/data");

        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Equal("00D0C9AABBCC", r.Identificador);
    }

    [Theory]
    [InlineData("Advantech/00D0C9AABBCC/Device_Status")]
    [InlineData("Advantech/00D0C9AABBCC/ctl/do1")]
    [InlineData("Advantech/00D0C9AABBCC/data/extra")]
    [InlineData("OutroFabricante/00D0C9AABBCC/data")]
    [InlineData("Advantech/XYZ/data")]
    public void OutrosTopicos_SaoIgnorados(string topico)
    {
        var r = Interpretar("{\"di1\":true}", topico);

        Assert.Equal(TipoResultadoWise.Ignorada, r.Tipo);
        Assert.Null(r.Amostra);
    }

    // ── Conteúdo ────────────────────────────────────────────────────

    [Fact]
    public void MensagemCompleta_ContadoresEEstadosNosCanaisCertos_HoraDeChegada()
    {
        // S1..S8 = di1..di8. Contadores: S2, S3, S5, S6. Estados: S1, S4, S7, S8.
        var r = Interpretar("""
            {"s":1,"t":"1970-01-01T07:56:43Z","q":192,"c":3,
             "di1":false,"di2":152340,"di3":87,"di4":true,
             "di5":0,"di6":4294967295,"di7":true,"di8":false}
            """);

        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Empty(r.Avisos);
        var a = r.Amostra!;
        Assert.Equal(Chegada, a.TimestampUtc); // a hora do WISE (1970) é ignorada
        Assert.Equal(152340u, a.Contadores[CanalWise.S2]);
        Assert.Equal(87u, a.Contadores[CanalWise.S3]);
        Assert.Equal(0u, a.Contadores[CanalWise.S5]);
        Assert.Equal(uint.MaxValue, a.Contadores[CanalWise.S6]);
        Assert.Equal(4, a.Contadores.Count);
        Assert.False(a.Estados[CanalWise.S1]);
        Assert.True(a.Estados[CanalWise.S4]);
        Assert.True(a.Estados[CanalWise.S7]);
        Assert.False(a.Estados[CanalWise.S8]);
        Assert.Equal(4, a.Estados.Count);
    }

    [Fact]
    public void SensorDeEstado_AceitaZeroEUm()
    {
        var a = Interpretar("{\"di1\":1,\"di8\":0}").Amostra!;

        Assert.True(a.Estados[CanalWise.S1]);
        Assert.False(a.Estados[CanalWise.S8]);
    }

    [Fact]
    public void SoOsSensoresMudaram_AmostraSemContadores()
    {
        // C.O.S. de um sensor: pode vir só ele.
        var a = Interpretar("{\"s\":1,\"q\":192,\"di8\":true}").Amostra!;

        Assert.Empty(a.Contadores);
        Assert.True(Assert.Single(a.Estados).Value);
    }

    [Fact]
    public void SemNenhumaEntrada_AindaEhAmostra_ProvaQueOWiseEstaVivo()
    {
        var r = Interpretar("{\"s\":1,\"t\":\"2026-09-28T12:00:00Z\",\"q\":192,\"c\":0}");

        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Empty(r.Amostra!.Contadores);
        Assert.Empty(r.Amostra.Estados);
    }

    [Fact]
    public void ContadorComTrueFalse_DescartaOCanal_EAvisa()
    {
        // Exemplo publicado de um WISE com todas as entradas em modo DI (não contador).
        var r = Interpretar("{\"s\":8,\"t\":\"1970-01-01T07:56:43Z\",\"q\":192,\"c\":0,\"di1\":false,\"di2\":true}");

        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Empty(r.Amostra!.Contadores);
        Assert.False(r.Amostra.Estados[CanalWise.S1]);
        var aviso = Assert.Single(r.Avisos);
        Assert.Contains("di2", aviso);
        Assert.Contains("modo contador", aviso);
    }

    [Theory]
    [InlineData("{\"di2\":-5}")]
    [InlineData("{\"di2\":12.5}")]
    [InlineData("{\"di2\":4294967296}")]
    [InlineData("{\"di2\":\"150\"}")]
    [InlineData("{\"di8\":2}")]
    [InlineData("{\"di8\":\"true\"}")]
    public void ValorForaDoEsperado_DescartaSoAqueleCanal(string payload)
    {
        var r = Interpretar(payload);

        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Empty(r.Amostra!.Contadores);
        Assert.Empty(r.Amostra.Estados);
        Assert.Single(r.Avisos);
    }

    [Fact]
    public void CamposQueNaoSaoEntradas_SaoIgnorados()
    {
        var a = Interpretar("{\"di0\":true,\"di9\":true,\"do1\":true,\"ai1\":12.3,\"DI1\":true}").Amostra!;

        Assert.Empty(a.Contadores);
        Assert.Empty(a.Estados);
    }

    // ── Conteúdo ilegível ───────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("nao e json")]
    [InlineData("{\"di1\":")]
    public void JsonInvalido_Invalida_ComIdentificador(string payload)
    {
        var r = Interpretar(payload);

        Assert.Equal(TipoResultadoWise.Invalida, r.Tipo);
        Assert.Equal("00D0C9AABBCC", r.Identificador);
        Assert.Null(r.Amostra);
        Assert.Contains("JSON", r.Motivo);
    }

    [Fact]
    public void JsonQueNaoEhObjeto_Invalida()
    {
        Assert.Equal(TipoResultadoWise.Invalida, Interpretar("[1,2,3]").Tipo);
    }

    // ── Ponta a ponta com a máquina de estados ─────────────────────

    [Fact]
    public void AmostrasInterpretadas_AlimentamOEstadoDaMaquina()
    {
        var config = new ConfiguracaoColetaIot(
            [new(CanalWise.S2, 12), new(CanalWise.S8)],
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90), []);
        var estado = new EstadoMaquinaIot(config);

        AmostraWise Em(int segundos, string payload) =>
            ParserWise.Interpretar(new MensagemMqtt("WISE", "Advantech/00D0C9AABBCC/data",
                Encoding.UTF8.GetBytes(payload), Chegada.AddSeconds(segundos))).Amostra!;

        estado.Processar(Em(0, "{\"di2\":1000,\"di8\":false}"));
        var eventos = estado.Processar(Em(20, "{\"di2\":1010,\"di8\":false}"));

        Assert.Equal(120, Assert.IsType<SmartLine.Core.Coleta.ProducaoApurada>(Assert.Single(eventos)).Garrafas);
    }
}
