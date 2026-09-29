using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Coleta;
using static SmartLine.Tests.Iot.Cenario;

namespace SmartLine.Tests.Iot;

public class LeiturasEntradasWiseTests
{
    private const string Ip = "192.168.10.21";
    private readonly LeiturasEntradasWise _leituras = new();

    [Fact]
    public void PrimeiraLeitura_SemIncremento()
    {
        _leituras.Registrar(Ip, Amostra(0, s2: 1000));

        var l = _leituras.Obter(Ip)!;
        Assert.Equal((1000u, (long?)null, 1L), (l.Contadores[CanalWise.S2].Valor, l.Contadores[CanalWise.S2].Incremento, l.Mensagens));
        Assert.Null(_leituras.Obter("192.168.10.22"));
    }

    [Fact]
    public void LeituraSeguinte_MostraQuantoSubiu_EEmQuantoTempo()
    {
        _leituras.Registrar(Ip, Amostra(0, s2: 1000, s3: 5));
        _leituras.Registrar(Ip, Amostra(20, s2: 1350));

        var l = _leituras.Obter(Ip)!;
        var s2 = l.Contadores[CanalWise.S2];
        Assert.Equal((1350u, (long?)350, (double?)20), (s2.Valor, s2.Incremento, s2.IntervaloSegundos));
        Assert.Equal(5u, l.Contadores[CanalWise.S3].Valor); // não veio agora: fica o último
        Assert.Equal((Em(20), 2L), (l.UltimaMensagemUtc, l.Mensagens));
    }

    [Fact]
    public void ContadorQueVoltou_NaoMostraIncremento()
    {
        _leituras.Registrar(Ip, Amostra(0, s2: 1000));
        _leituras.Registrar(Ip, Amostra(20, s2: 10));

        Assert.Null(_leituras.Obter(Ip)!.Contadores[CanalWise.S2].Incremento);
    }

    [Fact]
    public void Sensores_FicaOUltimoEstado()
    {
        _leituras.Registrar(Ip, Amostra(0, faltaGarrafas: false));
        _leituras.Registrar(Ip, Amostra(5, faltaGarrafas: true));

        Assert.True(_leituras.Obter(Ip)!.Estados[CanalWise.S8]);
    }
}

public class EntradasAoVivoTests
{
    private static readonly IReadOnlyList<EntradaWiseDto> Textos = EntradasAoVivo.TextosPadrao();

    [Fact]
    public void SemLeitura_AsOitoEntradas_NaoRecebidas()
    {
        var entradas = EntradasAoVivo.Montar(Textos, null);

        Assert.Equal(8, entradas.Count);
        Assert.All(entradas, e => Assert.False(e.Recebida));
    }

    [Fact]
    public void Sensores_EmTexto_ConformeAtivoOuNormal()
    {
        var leituras = new LeiturasEntradasWise();
        leituras.Registrar("ip", Amostra(0, s2: 100, faltaGarrafas: true, saidaGarrafasBloqueada: false));

        var entradas = EntradasAoVivo.Montar(Textos, leituras.Obter("ip")).ToDictionary(e => e.Canal);

        Assert.Equal((true, true, "Falta de garrafas na entrada"), (entradas[CanalWise.S8].ValorBruto!.Value, entradas[CanalWise.S8].EmAlarme!.Value, entradas[CanalWise.S8].Texto));
        Assert.Equal((false, "Saída de garrafas livre"), (entradas[CanalWise.S7].EmAlarme!.Value, entradas[CanalWise.S7].Texto));
        Assert.Equal((true, 100u), (entradas[CanalWise.S2].Recebida, entradas[CanalWise.S2].Valor!.Value));
        Assert.False(entradas[CanalWise.S5].Recebida);
    }

    [Fact]
    public void TextosPersonalizados_SaoUsados()
    {
        var textos = Textos.Select(t => t.Canal == CanalWise.S8 ? t with { Nome = "Entrada rotuladora", TextoAtivo = "Rotuladora vazia" } : t).ToList();
        var leituras = new LeiturasEntradasWise();
        leituras.Registrar("ip", Amostra(0, faltaGarrafas: true));

        var s8 = EntradasAoVivo.Montar(textos, leituras.Obter("ip")).Single(e => e.Canal == CanalWise.S8);

        Assert.Equal(("Entrada rotuladora", "Rotuladora vazia"), (s8.Nome, s8.Texto));
    }

    [Fact]
    public void Situacao_DoWise()
    {
        var wise = new WiseCadastrado(Guid.NewGuid(), "WISE", "192.168.10.21", true, Guid.NewGuid(), Guid.NewGuid(), null);
        var conectados = new HashSet<string> { "192.168.10.21" };

        Assert.Equal(SituacaoConexaoWise.Conectado, EntradasAoVivo.Situacao(wise, conectados));
        Assert.Equal(SituacaoConexaoWise.Desconectado, EntradasAoVivo.Situacao(wise, new HashSet<string>()));
        Assert.Equal(SituacaoConexaoWise.NaoCadastrado, EntradasAoVivo.Situacao(wise with { Ativo = false }, conectados));
        Assert.Equal(SituacaoConexaoWise.NaoCadastrado, EntradasAoVivo.Situacao(null, conectados));
    }
}
