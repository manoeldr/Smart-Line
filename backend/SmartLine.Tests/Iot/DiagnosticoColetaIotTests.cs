using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Simulacao;
using SmartLine.Tests.Infra;

namespace SmartLine.Tests.Iot;

/// <summary>O que a tela de dispositivos mostra: quem está conectado e quem publica sem cadastro.</summary>
public class DiagnosticoColetaIotTests : IAsyncLifetime
{
    private readonly int _porta = PortaLivre();
    private AmbienteColetaIot _amb = null!;
    private BrokerMqttService _broker = null!;

    public async Task InitializeAsync()
    {
        _amb = new AmbienteColetaIot(TimeProvider.System);
        _broker = new BrokerMqttService(new OpcoesBrokerMqtt { Porta = _porta }, _amb.Caixa, TimeProvider.System,
            NullLogger<BrokerMqttService>.Instance);
        await _broker.StartAsync(CancellationToken.None);
        await _amb.SubirAsync();
    }

    public async Task DisposeAsync()
    {
        await _broker.StopAsync(CancellationToken.None);
        _broker.Dispose();
        await _amb.DisposeAsync();
    }

    private static int PortaLivre()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var porta = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return porta;
    }

    private PublicadorWiseSimulado Wise(int numero, string ip) =>
        new(new MaquinaSimulada(numero, 600, 0, DateTime.UtcNow), ip, "127.0.0.1", _porta);

    [Fact]
    public async Task Broker_SabeQuaisIpsEstaoConectados()
    {
        using var wise = Wise(1, AmbienteColetaIot.IpA);
        await wise.PublicarAsync(DateTime.UtcNow);
        await AmbienteColetaIot.AguardarAsync(() => _broker.IpsConectados().Contains(AmbienteColetaIot.IpA),
            "o broker anotar a conexão");

        wise.Maquina.MudarCenario(CenarioSimulado.Desligado, DateTime.UtcNow);
        await wise.PublicarAsync(DateTime.UtcNow); // desligado: derruba a conexão

        await AmbienteColetaIot.AguardarAsync(() => !_broker.IpsConectados().Contains(AmbienteColetaIot.IpA),
            "o broker perceber a desconexão");
    }

    [Fact]
    public async Task WiseSemCadastro_AparecePraCadastrar_ESomeDepoisDeCadastrado()
    {
        using var novo = Wise(9, "127.0.0.29");
        await novo.PublicarAsync(DateTime.UtcNow);
        await _amb.AguardarMensagensAsync(1);

        var desconhecido = Assert.Single(_amb.Servico.WiseDesconhecidos());
        Assert.Equal(("127.0.0.29", "SIM-WISE-9"), (desconhecido.EnderecoIp, desconhecido.ClientId));

        _amb.Alterar(db => db.DispositivosIot.Single(d => d.EnderecoIp == AmbienteColetaIot.IpB).EnderecoIp = "127.0.0.29");
        await novo.PublicarAsync(DateTime.UtcNow);
        await _amb.AguardarMensagensAsync(2);

        Assert.Empty(_amb.Servico.WiseDesconhecidos());
    }
}
