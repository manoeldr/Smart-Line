using System.Net.NetworkInformation;
using SmartLine.Iot.Rede;

namespace SmartLine.Tests.Iot;

public class TestePingTests
{
    [Fact]
    public void Resumo_ComoOPingDoWindows()
    {
        var r = new ResultadoPing("192.168.10.21",
        [
            new RespostaPing(1, true, 2, "Resposta"),
            new RespostaPing(2, false, null, "Tempo esgotado"),
            new RespostaPing(3, true, 5, "Resposta"),
            new RespostaPing(4, true, 1, "Resposta")
        ]);

        Assert.Equal((4, 3, 25), (r.Enviados, r.Recebidos, r.PerdaPercentual));
        Assert.Equal(((long?)1, (long?)5, (long?)3), (r.TempoMinimoMs, r.TempoMaximoMs, r.TempoMedioMs));
    }

    [Fact]
    public void Resumo_SemNenhumaResposta_SemTempos()
    {
        var r = new ResultadoPing("192.168.10.21",
            Enumerable.Range(1, 4).Select(i => new RespostaPing(i, false, null, "Tempo esgotado")).ToList());

        Assert.Equal((0, 100), (r.Recebidos, r.PerdaPercentual));
        Assert.Equal(((long?)null, (long?)null, (long?)null), (r.TempoMinimoMs, r.TempoMaximoMs, r.TempoMedioMs));
    }

    [Fact]
    public void Situacoes_EmPortugues()
    {
        Assert.Equal("Tempo esgotado", TestePing.Descrever(IPStatus.TimedOut));
        Assert.Equal("Host de destino inacessível", TestePing.Descrever(IPStatus.DestinationHostUnreachable));
    }

    [Fact]
    public async Task PingNoProprioPc_Responde()
    {
        var r = await new TestePing().PingarAsync("127.0.0.1");

        Assert.Equal(TestePing.Tentativas, r.Enviados);
        Assert.Equal(TestePing.Tentativas, r.Recebidos);
        Assert.Equal(new[] { 1, 2, 3, 4 }, r.Respostas.Select(x => x.Sequencia));
    }
}
