using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Protocol;
using SmartLine.Iot.Broker;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Broker de verdade numa porta livre da máquina, com um cliente MQTT de
/// verdade fazendo o papel do WISE.
/// </summary>
public class BrokerMqttServiceTests : IAsyncLifetime
{
    private readonly int _porta = PortaLivre();
    private readonly CaixaDeEntradaMqtt _caixa = new();
    private BrokerMqttService _broker = null!;

    public async Task InitializeAsync()
    {
        _broker = Novo(new OpcoesBrokerMqtt { Porta = _porta });
        await _broker.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _broker.StopAsync(CancellationToken.None);
        _broker.Dispose();
    }

    private BrokerMqttService Novo(OpcoesBrokerMqtt opcoes) =>
        new(opcoes, _caixa, TimeProvider.System, NullLogger<BrokerMqttService>.Instance);

    private static int PortaLivre()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var porta = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return porta;
    }

    private async Task<IMqttClient> ConectarComoWise(string clientId)
    {
        var cliente = new MqttClientFactory().CreateMqttClient();
        await cliente.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _porta)
            .WithClientId(clientId)
            .Build());
        return cliente;
    }

    private static Task Publicar(IMqttClient cliente, string topico, string payload) =>
        cliente.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topico)
            .WithPayload(payload)
            // QoS 1: o PublishAsync só volta depois do broker confirmar, então a
            // mensagem já está na caixa quando o teste vai ler.
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());

    private async Task<MensagemMqtt> Proxima()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _caixa.Leitor.ReadAsync(limite.Token);
    }

    [Fact]
    public void SobeNaPortaConfigurada()
    {
        Assert.True(_broker.EmExecucao);
    }

    [Fact]
    public async Task MensagemPublicada_ChegaNaCaixaComClientIdTopicoEConteudo()
    {
        using var wise = await ConectarComoWise("WISE-4051-ENCH");
        var antes = DateTime.UtcNow;

        await Publicar(wise, "Advantech/00D0C9AABBCC/data", "{\"di1\":true}");

        var m = await Proxima();
        Assert.Equal("127.0.0.1", m.EnderecoIp); // o cliente conectou por 127.0.0.1
        Assert.Equal("WISE-4051-ENCH", m.ClientId);
        Assert.Equal("Advantech/00D0C9AABBCC/data", m.Topico);
        Assert.Equal("{\"di1\":true}", m.PayloadComoTexto);
        Assert.Equal(DateTimeKind.Utc, m.RecebidaEmUtc.Kind);
        Assert.InRange(m.RecebidaEmUtc, antes.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task VariosDispositivos_CadaMensagemComSeuClientId_NaOrdemDeChegada()
    {
        using var enchedora = await ConectarComoWise("WISE-ENCH");
        using var lavadora = await ConectarComoWise("WISE-LAV");

        await Publicar(enchedora, "t/1", "a");
        await Publicar(lavadora, "t/2", "b");
        await Publicar(enchedora, "t/1", "c");

        var recebidas = new[] { await Proxima(), await Proxima(), await Proxima() }
            .Select(m => (m.ClientId, m.PayloadComoTexto));

        Assert.Equal(new[] { ("WISE-ENCH", "a"), ("WISE-LAV", "b"), ("WISE-ENCH", "c") }, recebidas);
    }

    [Fact]
    public async Task Desabilitado_NaoAbreAPorta()
    {
        using var broker = Novo(new OpcoesBrokerMqtt { Habilitado = false, Porta = PortaLivre() });

        await broker.StartAsync(CancellationToken.None);

        Assert.False(broker.EmExecucao);
        await broker.StopAsync(CancellationToken.None); // não pode falhar
    }

    [Fact]
    public async Task PortaOcupada_NaoDerrubaOBackend()
    {
        // Mesma porta do broker que já está rodando neste teste.
        using var segundo = Novo(new OpcoesBrokerMqtt { Porta = _porta });

        var erro = await Record.ExceptionAsync(() => segundo.StartAsync(CancellationToken.None));

        Assert.Null(erro);
        Assert.False(segundo.EmExecucao);
        Assert.True(_broker.EmExecucao); // o primeiro segue de pé
    }
}

public class CaixaDeEntradaMqttTests
{
    private static MensagemMqtt Mensagem(int n) =>
        new("WISE", "192.168.10.21", "t", Encoding.UTF8.GetBytes(n.ToString()), DateTime.UtcNow);

    [Fact]
    public void FilaCheia_DescartaAMaisAntiga_EConta()
    {
        var caixa = new CaixaDeEntradaMqtt();

        for (var i = 0; i < CaixaDeEntradaMqtt.Capacidade + 3; i++)
            caixa.Publicar(Mensagem(i));

        Assert.Equal(3, caixa.Descartadas);
        Assert.True(caixa.Leitor.TryRead(out var primeira));
        Assert.Equal("3", primeira.PayloadComoTexto); // 0, 1 e 2 foram descartadas
    }
}

public class EnderecoRedeTests
{
    [Theory]
    [InlineData("192.168.10.21", "192.168.10.21")]
    [InlineData("  192.168.10.21 ", "192.168.10.21")]
    [InlineData("::ffff:192.168.10.21", "192.168.10.21")] // IPv4 visto por socket IPv6
    [InlineData("fe80::1", "fe80::1")]
    public void Texto_ViraFormaCanonica(string texto, string esperado)
    {
        Assert.Equal(esperado, EnderecoRede.Normalizar(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wise-enchedora")]
    [InlineData("192.168.10")]
    [InlineData("10")]
    [InlineData("192.168.10.300")]
    public void TextoQueNaoEhIp_Nulo(string? texto)
    {
        Assert.Null(EnderecoRede.Normalizar(texto));
    }

    [Fact]
    public void EndpointIpv4MapeadoEmIpv6_ViraIpv4()
    {
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("192.168.10.21").MapToIPv6(), 51234);

        Assert.Equal("192.168.10.21", EnderecoRede.Normalizar(endpoint));
    }

    [Fact]
    public void EndpointQueNaoEhIp_Nulo()
    {
        Assert.Null(EnderecoRede.Normalizar(new System.Net.DnsEndPoint("wise.local", 1883)));
        Assert.Null(EnderecoRede.Normalizar((System.Net.EndPoint?)null));
    }
}
