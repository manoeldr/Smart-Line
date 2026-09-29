using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;
using SmartLine.Iot.Simulacao;
using SmartLine.Tests.Infra;

namespace SmartLine.Tests.Iot;

/// <summary>O que a tela de dispositivos mostra: quem está conectado, quem publica e em que medição.</summary>
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
    public async Task ListaDeWise_JuntaBrokerMotorEMedicoes()
    {
        using var novo = Wise(9, "127.0.0.29");
        await novo.PublicarAsync(DateTime.UtcNow);
        await _amb.AguardarMensagensAsync(1);
        _amb.CadastrarWise("127.0.0.29");
        await _amb.IniciarColetaAsync(_amb.MaquinaB, "127.0.0.29");

        var medicoes = await _amb.WisesEmMedicaoAsync();
        var lista = ListaWise.Montar([], _broker.Conexoes(), _amb.Servico.WiseVistos(), medicoes);

        var wise = Assert.Single(lista);
        Assert.Equal(("127.0.0.29", true, "SIM-WISE-9", 1L), (wise.EnderecoIp, wise.Conectado, wise.ClientId, wise.Mensagens));
        Assert.Equal((_amb.MaquinaB, "Enchedora", "Linha 1", "Cliente", "Auditor"),
            (wise.Medicao!.MaquinaLinhaId, wise.Medicao.Maquina, wise.Medicao.Linha, wise.Medicao.Cliente, wise.Medicao.Usuario));
    }
}
