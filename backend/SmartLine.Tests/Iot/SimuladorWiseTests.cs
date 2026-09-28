using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Simulacao;
using SmartLine.Iot.Wise;

namespace SmartLine.Tests.Iot;

public class MaquinaSimuladaTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 13, 0, 0, DateTimeKind.Utc);

    private static MaquinaSimulada Nova(double ritmo = 600, double rejeito = 1) => new(1, ritmo, rejeito, T0);

    /// <summary>O que o SmartLine entende da mensagem gerada.</summary>
    private static AmostraWise Ler(string payload, DateTime quando)
    {
        var r = ParserWise.Interpretar(new MensagemMqtt("SIM", "127.0.0.21", "Advantech/SIMULADO0001/data", Encoding.UTF8.GetBytes(payload), quando));
        Assert.Equal(TipoResultadoWise.Dados, r.Tipo);
        Assert.Empty(r.Avisos);
        return r.Amostra!;
    }

    [Fact]
    public void Rodando_ContaNoRitmo_ERejeitoProporcional()
    {
        var m = Nova(ritmo: 600, rejeito: 2);

        var a = Ler(m.GerarPayload(T0.AddMinutes(1)), T0.AddMinutes(1));

        Assert.Equal(600u, a.Contadores[CanalWise.S2]);
        Assert.Equal(12u, a.Contadores[CanalWise.S3]);
    }

    [Fact]
    public void ProducaoDependeDoTempo_NaoDoNumeroDeMensagens()
    {
        var poucas = Nova();
        var muitas = Nova();

        poucas.GerarPayload(T0.AddMinutes(10));
        for (var s = 1; s <= 600; s++)
            muitas.GerarPayload(T0.AddSeconds(s));

        Assert.Equal(poucas.ContadorProducao, muitas.ContadorProducao);
    }

    [Fact]
    public void RodandoOsSensoresFicamNormais()
    {
        var a = Ler(Nova().GerarPayload(T0), T0);

        // Invertidos: normal = S1/S8 em 0 (com presença), S4/S7 em 1 (sem acúmulo).
        Assert.False(a.Estados[CanalWise.S1]);
        Assert.True(a.Estados[CanalWise.S4]);
        Assert.True(a.Estados[CanalWise.S7]);
        Assert.False(a.Estados[CanalWise.S8]);
        Assert.All(a.Estados, e => Assert.False(MapaWise.Definicao(e.Key).EstaEmAlarme(e.Value)));
    }

    [Theory]
    [InlineData(CenarioSimulado.FaltaGarrafas, CanalWise.S8)]
    [InlineData(CenarioSimulado.AbaixoAcumuloMinimo, CanalWise.S1)]
    [InlineData(CenarioSimulado.SaidaGarrafasBloqueada, CanalWise.S7)]
    [InlineData(CenarioSimulado.SaidaCaixasBloqueada, CanalWise.S4)]
    public void CadaCenarioDeFalha_ParaAProducao_EAlarmaSoOSensorDele(CenarioSimulado cenario, CanalWise sensor)
    {
        var m = Nova();
        m.MudarCenario(cenario, T0.AddMinutes(1)); // 600 produzidas até aqui

        var a = Ler(m.GerarPayload(T0.AddMinutes(5)), T0.AddMinutes(5));

        Assert.Equal(600u, a.Contadores[CanalWise.S2]);
        var emAlarme = a.Estados.Where(e => MapaWise.Definicao(e.Key).EstaEmAlarme(e.Value)).Select(e => e.Key);
        Assert.Equal(new[] { sensor }, emAlarme);
    }

    [Fact]
    public void ParadaSemCausa_ParaComTodosOsSensoresNormais()
    {
        var m = Nova();
        m.MudarCenario(CenarioSimulado.ParadaSemCausa, T0.AddMinutes(1));

        var a = Ler(m.GerarPayload(T0.AddMinutes(5)), T0.AddMinutes(5));

        Assert.Equal(600u, a.Contadores[CanalWise.S2]);
        Assert.All(a.Estados, e => Assert.False(MapaWise.Definicao(e.Key).EstaEmAlarme(e.Value)));
    }

    [Fact]
    public void Reiniciar_ZeraOsContadores()
    {
        var m = Nova();
        m.GerarPayload(T0.AddMinutes(1));

        m.Reiniciar(T0.AddMinutes(1));

        Assert.Equal(0u, Ler(m.GerarPayload(T0.AddMinutes(1)), T0.AddMinutes(1)).Contadores[CanalWise.S2]);
    }

    [Fact]
    public void Desligado_NaoPublica_ENaoProduz()
    {
        var m = Nova();
        m.MudarCenario(CenarioSimulado.Desligado, T0);

        m.Avancar(T0.AddMinutes(10));

        Assert.False(m.Publica);
        Assert.Equal(0u, m.ContadorProducao);
    }

    [Fact]
    public void Contador_ViraNoLimiteDoUint_ComoOWiseReal()
    {
        // 2^32 pulsos em 1 minuto: o contador dá a volta e fica em 0.
        var m = new MaquinaSimulada(1, 4_294_967_296d + 5, 0, T0);

        m.Avancar(T0.AddMinutes(1));

        Assert.Equal(5u, m.ContadorProducao);
    }

    [Fact]
    public void PontaAPonta_FaltaDeGarrafasSimulada_ViraParadaExternaClassificada()
    {
        var config = new ConfiguracaoColetaIot(
            [new(CanalWise.S2), new(CanalWise.S3), new(CanalWise.S1), new(CanalWise.S4), new(CanalWise.S7), new(CanalWise.S8)],
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90), Cenario.RegrasPadrao);
        var estado = new EstadoMaquinaIot(config);
        var m = Nova();
        var eventos = new List<EventoColeta>();

        void Publicar(int segundos) =>
            eventos.AddRange(estado.Processar(Ler(m.GerarPayload(T0.AddSeconds(segundos)), T0.AddSeconds(segundos))));

        for (var s = 0; s <= 60; s += 20) Publicar(s);                        // rodando
        m.MudarCenario(CenarioSimulado.FaltaGarrafas, T0.AddSeconds(60));
        for (var s = 61; s <= 141; s += 20) Publicar(s);                      // parada confirmada após Z

        var parada = Assert.Single(eventos.OfType<ParadaIniciada>());
        Assert.Equal(T0.AddSeconds(60), parada.InstanteUtc);
        Assert.Equal(TipoParada.Externa, parada.Classificacao.Tipo);
        Assert.Equal(Cenario.MotivoFaltaGarrafas, parada.Classificacao.MotivoParadaId);
    }
}

/// <summary>Simulador de verdade publicando num broker de verdade.</summary>
public class PublicadorWiseSimuladoTests : IAsyncLifetime
{
    private readonly int _porta = PortaLivre();
    private readonly CaixaDeEntradaMqtt _caixa = new();
    private BrokerMqttService _broker = null!;

    public async Task InitializeAsync()
    {
        _broker = new BrokerMqttService(new OpcoesBrokerMqtt { Porta = _porta }, _caixa, TimeProvider.System,
            NullLogger<BrokerMqttService>.Instance);
        await _broker.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _broker.StopAsync(CancellationToken.None);
        _broker.Dispose();
    }

    private static int PortaLivre()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var porta = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return porta;
    }

    private async Task<MensagemMqtt> Proxima()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _caixa.Leitor.ReadAsync(limite.Token);
    }

    private PublicadorWiseSimulado Novo(int numero, string origem) =>
        new(new MaquinaSimulada(numero, 600, 1, DateTime.UtcNow), origem, "127.0.0.1", _porta);

    [Fact]
    public async Task CadaMaquinaSimulada_ChegaComOProprioIp()
    {
        using var enchedora = Novo(1, "127.0.0.21");
        using var lavadora = Novo(2, "127.0.0.22");

        await enchedora.PublicarAsync(DateTime.UtcNow);
        await lavadora.PublicarAsync(DateTime.UtcNow);

        var m1 = await Proxima();
        var m2 = await Proxima();
        Assert.Equal(("127.0.0.21", "Advantech/SIMULADO0001/data"), (m1.EnderecoIp, m1.Topico));
        Assert.Equal(("127.0.0.22", "Advantech/SIMULADO0002/data"), (m2.EnderecoIp, m2.Topico));
        Assert.Equal(TipoResultadoWise.Dados, ParserWise.Interpretar(m1).Tipo);
    }

    [Fact]
    public async Task Desligado_DerrubaAConexao_EVoltaAoReligar()
    {
        using var wise = Novo(1, "127.0.0.21");
        await wise.PublicarAsync(DateTime.UtcNow);
        await Proxima();

        wise.Maquina.MudarCenario(CenarioSimulado.Desligado, DateTime.UtcNow);
        Assert.Null(await wise.PublicarAsync(DateTime.UtcNow));
        Assert.False(wise.Conectado);

        wise.Maquina.MudarCenario(CenarioSimulado.Rodando, DateTime.UtcNow);
        Assert.NotNull(await wise.PublicarAsync(DateTime.UtcNow));
        Assert.True(wise.Conectado);
        Assert.Equal("127.0.0.21", (await Proxima()).EnderecoIp);
    }
}
