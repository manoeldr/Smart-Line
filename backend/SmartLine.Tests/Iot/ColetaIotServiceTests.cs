using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Simulacao;
using SmartLine.Tests.Infra;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Motor da coleta com mensagens entregues direto na caixa de entrada, com
/// instantes escolhidos: horas de operação em milissegundos.
/// </summary>
public class ColetaIotServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 11, 0, 0, TimeSpan.Zero);
    private static DateTime Em(int segundos) => T0.UtcDateTime.AddSeconds(segundos);

    private readonly FakeTimeProvider _tempo = new(T0);
    private AmbienteColetaIot _amb = null!;
    private long _enviadas;

    public Task InitializeAsync()
    {
        _amb = new AmbienteColetaIot(_tempo);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _amb.DisposeAsync();

    private void Enviar(string ip, int segundos, uint s2, uint s3 = 0, bool falta = false)
    {
        _amb.Enviar(ip, Em(segundos), s2, s3, falta);
        _enviadas++;
    }

    private Task Aguardar() => _amb.AguardarMensagensAsync(_enviadas);

    [Fact]
    public async Task ColetaIniciada_PrimeiraMensagemEReferencia_DepoisSomaAProducao()
    {
        await _amb.SubirAsync();
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);

        Enviar(AmbienteColetaIot.IpA, 0, s2: 1000, s3: 10);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 1600, s3: 16);
        await Aguardar();

        var s = _amb.Servico.Situacao(_amb.MaquinaA)!;
        Assert.Equal(acompanhamento, s.AcompanhamentoId);
        Assert.Equal(SituacaoMaquina.Rodando, s.Situacao);
        Assert.Equal(new ProducaoPendente(600, 6), s.ProducaoPendente);
        Assert.Equal(1600u, s.Contadores[CanalWise.S2]);
        Assert.Equal(2, _amb.Servico.MensagensProcessadas);
    }

    [Fact]
    public async Task FaltaDeGarrafas_AbreParadaClassificada_EFechaQuandoVoltaAProduzir()
    {
        await _amb.SubirAsync();
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);

        Enviar(AmbienteColetaIot.IpA, 0, s2: 0);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 200);
        Enviar(AmbienteColetaIot.IpA, 40, s2: 400);                // último incremento
        Enviar(AmbienteColetaIot.IpA, 60, s2: 400, falta: true);
        Enviar(AmbienteColetaIot.IpA, 100, s2: 400, falta: true);  // 60 s sem incremento: parada
        await Aguardar();

        var parada = Assert.Single(_amb.Paradas(acompanhamento));
        Assert.Equal(Em(40), parada.Inicio);
        Assert.Null(parada.Fim);
        Assert.Equal(("Falta de garrafas na entrada", TipoParada.Externa), (parada.Motivo!.Nome, parada.Motivo.Tipo));
        Assert.Equal(SituacaoMaquina.Parada, _amb.Servico.Situacao(_amb.MaquinaA)!.Situacao);

        Enviar(AmbienteColetaIot.IpA, 120, s2: 450);
        await Aguardar();

        Assert.Equal(Em(120), Assert.Single(_amb.Paradas(acompanhamento)).Fim);
        Assert.Equal(new ProducaoPendente(450, 0), _amb.Servico.Situacao(_amb.MaquinaA)!.ProducaoPendente);
    }

    [Fact]
    public async Task CadaMaquina_TemOProprioEstado()
    {
        await _amb.SubirAsync();
        var a = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        var b = await _amb.IniciarColetaAsync(_amb.MaquinaB);

        for (var i = 0; i <= 5; i++)
        {
            Enviar(AmbienteColetaIot.IpA, i * 20, s2: (uint)(i * 100));
            Enviar(AmbienteColetaIot.IpB, i * 20, s2: 5000, falta: true); // B parada desde o começo
        }
        await Aguardar();

        Assert.Equal((a, 500L), (_amb.Servico.Situacao(_amb.MaquinaA)!.AcompanhamentoId, _amb.Servico.Situacao(_amb.MaquinaA)!.ProducaoPendente.Garrafas));
        Assert.Equal((b, 0L), (_amb.Servico.Situacao(_amb.MaquinaB)!.AcompanhamentoId, _amb.Servico.Situacao(_amb.MaquinaB)!.ProducaoPendente.Garrafas));
        Assert.Empty(_amb.Paradas(a));
        Assert.Equal(Em(0), Assert.Single(_amb.Paradas(b)).Inicio);
    }

    [Fact]
    public async Task WiseLivre_Descarta_ERegistraUmaVezSo_SemAviso()
    {
        await _amb.SubirAsync();

        for (var i = 0; i < 3; i++)
            Enviar("10.0.0.99", i * 20, s2: 100);
        await Aguardar();

        Assert.Equal(3, _amb.Servico.MensagensDescartadas);
        Assert.Single(_amb.Log.Entradas, e => e.Nivel == LogLevel.Information && e.Texto.Contains("10.0.0.99"));
        Assert.DoesNotContain(_amb.Log.Entradas, e => e.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task OWiseEDaMedicao_NaoDaMaquina_DepoisDeFinalizarVaiParaOutraMaquina()
    {
        await _amb.SubirAsync();
        var naA = await _amb.IniciarColetaAsync(_amb.MaquinaA, AmbienteColetaIot.IpA);
        Enviar(AmbienteColetaIot.IpA, 0, s2: 0);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 100);
        await Aguardar();
        Assert.Equal(100, _amb.Servico.Situacao(_amb.MaquinaA)!.ProducaoPendente.Garrafas);

        await _amb.FinalizarColetaAsync(naA);
        var naB = await _amb.IniciarColetaAsync(_amb.MaquinaB, AmbienteColetaIot.IpA);
        Enviar(AmbienteColetaIot.IpA, 40, s2: 200); // referência da coleta na B
        Enviar(AmbienteColetaIot.IpA, 60, s2: 260);
        await Aguardar();

        var b = _amb.Servico.Situacao(_amb.MaquinaB)!;
        Assert.Equal((naB, 60L), (b.AcompanhamentoId, b.ProducaoPendente.Garrafas));
        await _amb.Servico.VerificarAgoraAsync();
        Assert.Null(_amb.Servico.Situacao(_amb.MaquinaA));
    }

    [Fact]
    public async Task WiseVistos_TodoIpQuePublicou_EmMedicaoOuLivre_ComContagem()
    {
        await _amb.SubirAsync();
        await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(AmbienteColetaIot.IpA, 0, s2: 0);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 10);
        Enviar("10.0.0.99", 30, s2: 5);
        await Aguardar();

        var vistos = _amb.Servico.WiseVistos();
        Assert.Equal(
            new[] { (AmbienteColetaIot.IpA, $"WISE-{AmbienteColetaIot.IpA}", Em(20), 2L), ("10.0.0.99", "WISE-10.0.0.99", Em(30), 1L) },
            vistos.Select(v => (v.EnderecoIp, v.ClientId, v.UltimaMensagemUtc, v.Mensagens)).OrderBy(v => v.EnderecoIp != AmbienteColetaIot.IpA));
    }

    [Fact]
    public async Task MaquinaSemColetaLigada_DescartaSemErro()
    {
        await _amb.SubirAsync();

        Enviar(AmbienteColetaIot.IpB, 0, s2: 100);
        await Aguardar();

        Assert.Null(_amb.Servico.Situacao(_amb.MaquinaB));
        Assert.Equal(1, _amb.Servico.MensagensDescartadas);
        Assert.DoesNotContain(_amb.Log.Entradas, e => e.Nivel >= LogLevel.Warning);
    }

    [Fact]
    public async Task ColetaFinalizada_DescartaOEstado_ENovaColetaComecaDoZero()
    {
        await _amb.SubirAsync();
        var primeira = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(AmbienteColetaIot.IpA, 0, s2: 0);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 100);
        await Aguardar();

        _tempo.Advance(TimeSpan.FromMinutes(1));
        await _amb.FinalizarColetaAsync(primeira);
        await _amb.Servico.VerificarAgoraAsync(); // o relógio percebe a coleta finalizada
        Assert.Null(_amb.Servico.Situacao(_amb.MaquinaA));

        _tempo.Advance(TimeSpan.FromMinutes(1));
        var segunda = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(AmbienteColetaIot.IpA, 180, s2: 300); // referência da nova coleta
        Enviar(AmbienteColetaIot.IpA, 200, s2: 350);
        await Aguardar();

        var s = _amb.Servico.Situacao(_amb.MaquinaA)!;
        Assert.Equal((segunda, 50L), (s.AcompanhamentoId, s.ProducaoPendente.Garrafas));
    }

    [Fact]
    public async Task AoSubir_RetomaAColeta_ECreditaOQueFoiProduzidoComOBackendFora()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        // Última consolidação antes da queda: S2 em 1000, às 11:00:10.
        _amb.Alterar(db =>
        {
            var s2 = db.AcompanhamentoCanais.Single(c => c.AcompanhamentoId == acompanhamento && c.Canal == CanalWise.S2);
            s2.UltimoValorBruto = 1000;
            s2.UltimoValorEm = Em(10);
        });
        _tempo.SetUtcNow(T0.AddMinutes(5)); // backend volta 5 min depois

        await _amb.SubirAsync();
        Assert.Equal(SituacaoMaquina.SemComunicacao, _amb.Servico.Situacao(_amb.MaquinaA)!.Situacao);

        Enviar(AmbienteColetaIot.IpA, 300, s2: 1500);
        await Aguardar();

        var s = _amb.Servico.Situacao(_amb.MaquinaA)!;
        Assert.Equal((SituacaoMaquina.Rodando, 500L), (s.Situacao, s.ProducaoPendente.Garrafas));
        var periodo = Assert.Single(_amb.Periodos(_amb.MaquinaA));
        Assert.Equal((Em(10), Em(300)), (periodo.Inicio, periodo.Fim!.Value));
    }

    [Fact]
    public async Task MensagemInvalida_EDescartada_ENaoAtrapalhaAsProximas()
    {
        await _amb.SubirAsync();
        await _amb.IniciarColetaAsync(_amb.MaquinaA);

        _amb.Enviar(AmbienteColetaIot.IpA, Em(0), "isto não é json");
        _amb.Enviar(AmbienteColetaIot.IpA, Em(0), "{}", topico: "Advantech/00D0C9FA1234/Device_Status");
        _enviadas += 2;
        Enviar(AmbienteColetaIot.IpA, 0, s2: 10);
        Enviar(AmbienteColetaIot.IpA, 20, s2: 70);
        await Aguardar();

        Assert.Equal(2, _amb.Servico.MensagensDescartadas);
        Assert.Equal(60, _amb.Servico.Situacao(_amb.MaquinaA)!.ProducaoPendente.Garrafas);
        Assert.Single(_amb.Log.Entradas, e => e.Nivel == LogLevel.Warning && e.Texto.Contains("fora do formato"));
    }

    [Fact]
    public async Task ValidarEntradas_GuardaALeitura_MesmoDeWiseLivre()
    {
        await _amb.SubirAsync();

        Enviar("10.0.0.99", 0, s2: 100);
        Enviar("10.0.0.99", 20, s2: 160, falta: true);
        await Aguardar();

        var l = _amb.Servico.LeiturasDoWise("10.0.0.99")!;
        Assert.Equal((160u, (long?)60, true), (l.Contadores[CanalWise.S2].Valor, l.Contadores[CanalWise.S2].Incremento, l.Estados[CanalWise.S8]));
        Assert.Null(_amb.Servico.LeiturasDoWise(AmbienteColetaIot.IpA));
    }
}

/// <summary>
/// Ponta a ponta de verdade: simulador publicando por TCP no broker, broker
/// enfileirando, motor gravando no banco. Relógio real, Z de 1 segundo.
/// </summary>
public class ColetaIotPontaAPontaTests : IAsyncLifetime
{
    private readonly int _porta = PortaLivre();
    private AmbienteColetaIot _amb = null!;
    private BrokerMqttService _broker = null!;

    public async Task InitializeAsync()
    {
        _amb = new AmbienteColetaIot(TimeProvider.System, tempoDeteccaoParadaSegundos: 1);
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

    [Fact]
    public async Task WiseSimulado_ProduzEParaPorFaltaDeGarrafas_ParadaGravadaNaMaquinaDoIp()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        using var wise = new PublicadorWiseSimulado(
            new MaquinaSimulada(1, pulsosPorMinuto: 6000, percentualRejeito: 0, DateTime.UtcNow),
            AmbienteColetaIot.IpA, "127.0.0.1", _porta);
        var publicadas = 0L;

        async Task Publicar()
        {
            await wise.PublicarAsync(DateTime.UtcNow);
            await _amb.AguardarMensagensAsync(++publicadas);
        }

        await Publicar();                 // referência
        await Task.Delay(300);
        await Publicar();                 // ~30 garrafas
        Assert.True(_amb.Servico.Situacao(_amb.MaquinaA)!.ProducaoPendente.Garrafas > 0);

        wise.Maquina.MudarCenario(CenarioSimulado.FaltaGarrafas, DateTime.UtcNow);
        await Publicar();                 // último incremento
        await Task.Delay(1300);
        await Publicar();                 // mais de 1 s sem incremento: parada

        var parada = Assert.Single(_amb.Paradas(acompanhamento));
        Assert.Null(parada.Fim);
        Assert.Equal("Falta de garrafas na entrada", parada.Motivo!.Nome);
        Assert.Equal(0, _amb.Servico.MensagensDescartadas);
    }
}
