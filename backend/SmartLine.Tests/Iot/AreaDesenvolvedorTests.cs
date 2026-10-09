using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Diagnostico;
using SmartLine.Iot.Rede;
using SmartLine.Iot.Simulacao;
using SmartLine.Iot.Wise;

namespace SmartLine.Tests.Iot;

/// <summary>Log em memória da Área do Desenvolvedor.</summary>
public class LogEmMemoriaTests
{
    private static ILogger Registrador(LogEmMemoria log, string categoria = "SmartLine.Teste") => log.CreateLogger(categoria);

    [Fact]
    public void GuardaAsLinhas_EmOrdem_ComSequenciaCrescente()
    {
        var log = new LogEmMemoria();
        var l = Registrador(log);
        l.LogInformation("primeira");
        l.LogWarning("segunda");

        var linhas = log.Linhas();

        Assert.Equal(new[] { "primeira", "segunda" }, linhas.Select(x => x.Mensagem));
        Assert.Equal(new[] { 1L, 2L }, linhas.Select(x => x.Sequencia));
        Assert.Equal(2, log.UltimaSequencia);
    }

    [Fact]
    public void PassouDaCapacidade_AsMaisAntigasSaem()
    {
        var log = new LogEmMemoria(capacidade: 3);
        var l = Registrador(log);
        for (var i = 1; i <= 5; i++) l.LogInformation("linha {N}", i);

        Assert.Equal(new[] { "linha 3", "linha 4", "linha 5" }, log.Linhas().Select(x => x.Mensagem));
    }

    [Fact]
    public void DepoisDe_TrazSoONovo()
    {
        var log = new LogEmMemoria();
        var l = Registrador(log);
        l.LogInformation("a");
        l.LogInformation("b");
        var ultima = log.UltimaSequencia;
        l.LogInformation("c");

        Assert.Equal(new[] { "c" }, log.Linhas(depoisDe: ultima).Select(x => x.Mensagem));
        Assert.Empty(log.Linhas(depoisDe: log.UltimaSequencia));
    }

    [Fact]
    public void FiltraPorNivel_EPorTexto_SemDiferencaDeMaiusculas()
    {
        var log = new LogEmMemoria();
        var l = Registrador(log, "SmartLine.Iot.Coleta.ColetaIotService");
        l.LogInformation("WISE 192.168.0.10 conectou");
        l.LogWarning("WISE 192.168.0.10 sem comunicação");
        l.LogError(new InvalidOperationException("falhou"), "Erro ao gravar");
        l.LogDebug("não entra");

        Assert.Equal(2, log.Linhas(nivelMinimo: LogLevel.Warning).Count);
        Assert.Single(log.Linhas(nivelMinimo: LogLevel.Error));
        Assert.Equal(2, log.Linhas(texto: "wise").Count);
        Assert.Single(log.Linhas(texto: "coletaiot", nivelMinimo: LogLevel.Error));
        Assert.Single(log.Linhas(texto: "falhou")); // na exceção
        Assert.Equal(3, log.Linhas().Count); // Debug fica fora
        Assert.Contains("falhou", log.Linhas(nivelMinimo: LogLevel.Error)[0].Excecao);
    }

    [Fact]
    public void Maximo_DevolveAsUltimas()
    {
        var log = new LogEmMemoria();
        var l = Registrador(log);
        for (var i = 1; i <= 10; i++) l.LogInformation("linha {N}", i);

        Assert.Equal(new[] { "linha 9", "linha 10" }, log.Linhas(maximo: 2).Select(x => x.Mensagem));
    }
}

/// <summary>O simulador inteiro (o que a Área do Desenvolvedor liga), contra um broker de teste.</summary>
public class SimuladorWiseEmbutidoTests : IAsyncLifetime
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

    // Sem interface HTTP (porta 80 exige privilégio) e sem automático (o teste manda).
    private OpcoesSimulador Opcoes(int maquinas = 2) => new(
        Maquinas: maquinas, IntervaloSegundos: 3600, Broker: "127.0.0.1", PortaBroker: _porta,
        PrimeiroIp: 31, Automatico: false, PortaHttp: 0);

    [Fact]
    public async Task Ligar_PublicaCadaMaquinaComOProprioIp_EDesligar_Fecha()
    {
        await using var simulador = new SimuladorWise();
        var avisos = new List<string>();
        simulador.Mensagem += avisos.Add;

        await simulador.LigarAsync(Opcoes());

        Assert.True(simulador.Ligado);
        var m1 = await Proxima();
        var m2 = await Proxima();
        Assert.Equal(new[] { "127.0.0.31", "127.0.0.32" }, new[] { m1.EnderecoIp, m2.EnderecoIp }.Order());
        Assert.Equal(TipoResultadoWise.Dados, ParserWise.Interpretar(m1).Tipo);

        var situacao = simulador.Situacao();
        Assert.Equal(2, situacao.Count);
        Assert.All(situacao, m => Assert.True(m.Conectada));
        Assert.All(situacao, m => Assert.False(m.Automatico));
        Assert.Contains(avisos, a => a.StartsWith("Simulador ligado"));

        await simulador.DesligarAsync();

        Assert.False(simulador.Ligado);
        Assert.Empty(simulador.Situacao());
        Assert.Contains("Simulador desligado.", avisos);
    }

    [Fact]
    public async Task Comando_MudaOCenario_EPublicaNaHora()
    {
        await using var simulador = new SimuladorWise();
        await simulador.LigarAsync(Opcoes(maquinas: 1));
        await Proxima(); // a inicial

        await simulador.ComandoAsync(1, AcaoSimulada.Falta);

        var m = simulador.Situacao().Single();
        Assert.Equal(CenarioSimulado.FaltaGarrafas, m.Cenario);
        var amostra = ParserWise.Interpretar(await Proxima()).Amostra!;
        Assert.True(MapaWise.Definicao(CanalWise.S8).EstaEmAlarme(amostra.Estados[CanalWise.S8])); // falta de garrafas na entrada
    }

    [Fact]
    public async Task Comando_SemRede_ParaDePublicar_ERodandoDevolve()
    {
        await using var simulador = new SimuladorWise();
        await simulador.LigarAsync(Opcoes(maquinas: 1));
        await Proxima();

        await simulador.ComandoAsync(1, AcaoSimulada.SemRede);
        Assert.Equal(FalhaComunicacao.SemRede, simulador.Situacao().Single().Falha);
        Assert.False(simulador.Situacao().Single().Conectada);

        await simulador.ComandoAsync(1, AcaoSimulada.Rodando);
        Assert.Null(simulador.Situacao().Single().Falha);
        Assert.Equal("127.0.0.31", (await Proxima()).EnderecoIp);
    }

    [Fact]
    public async Task Comando_Todas_EAutoManual()
    {
        await using var simulador = new SimuladorWise();
        await simulador.LigarAsync(Opcoes());

        await simulador.ComandoAsync(0, AcaoSimulada.Auto);
        Assert.All(simulador.Situacao(), m => Assert.True(m.Automatico));

        // Um comando de cenário tira a máquina do automático.
        await simulador.ComandoAsync(2, AcaoSimulada.Parada);
        Assert.False(simulador.Situacao()[1].Automatico);
        Assert.True(simulador.Situacao()[0].Automatico);

        await simulador.ComandoAsync(0, AcaoSimulada.Manual);
        Assert.All(simulador.Situacao(), m => Assert.False(m.Automatico));
    }

    [Fact]
    public async Task Comando_MaquinaQueNaoExiste_OuSimuladorDesligado_Recusa()
    {
        await using var simulador = new SimuladorWise();
        await Assert.ThrowsAsync<InvalidOperationException>(() => simulador.ComandoAsync(1, AcaoSimulada.Rodando));

        await simulador.LigarAsync(Opcoes(maquinas: 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => simulador.ComandoAsync(2, AcaoSimulada.Rodando));
    }

    [Fact]
    public async Task LigarDeNovo_TrocaAsOpcoes_SemPrecisarDesligar()
    {
        await using var simulador = new SimuladorWise();
        await simulador.LigarAsync(Opcoes(maquinas: 1));
        await simulador.LigarAsync(Opcoes(maquinas: 3));

        Assert.Equal(3, simulador.Situacao().Count);
        Assert.Equal(3, simulador.Opcoes!.Maquinas);
    }

    [Fact]
    public async Task ZerarContadores_DeUmWiseSimulado_NaoPassaPeloHttp_ENuncaFalha()
    {
        await using var simulador = new SimuladorWise();
        await simulador.LigarAsync(Opcoes(maquinas: 1)); // sem interface HTTP (PortaHttp = 0)
        await Proxima();

        // Deixa o contador andar e zera pelo caminho que o backend usa ao iniciar a medição.
        var maquina = simulador.Situacao().Single();
        using var real = new ZeradorContadoresWise(new OpcoesHttpWise { Porta = 1, Timeout = TimeSpan.FromMilliseconds(300) });
        var zerador = new ZeradorComSimulador(simulador, real);

        var r = await zerador.ZerarAsync(maquina.EnderecoIp, [CanalWise.S2, CanalWise.S3]);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(0u, simulador.Situacao().Single().ContadorProducao);

        // IP que não é simulado: segue para o HTTP de verdade (aqui ninguém escuta: falha).
        Assert.False((await zerador.ZerarAsync("127.0.0.250", [CanalWise.S2])).Sucesso);
    }

    [Fact]
    public void Opcoes_PulsosPorMinuto_DivideOsGarrafasPorPulso()
    {
        var o = new OpcoesSimulador(Maquinas: 4, GarrafasPorMinuto: [600], GarrafasPorPulso: [1, 1, 1, 84]);

        Assert.Equal(600, o.PulsosPorMinutoDa(1));
        Assert.Equal(600.0 / 84, o.PulsosPorMinutoDa(4), 6);
        Assert.Equal(600, o.GarrafasPorMinutoDa(4)); // lista curta repete o último
    }
}
