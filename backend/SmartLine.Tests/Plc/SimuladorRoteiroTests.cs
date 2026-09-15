using SmartLine.Core.Plc;
using SmartLine.Plc.Simulacao;

namespace SmartLine.Tests.Plc;

/// <summary>
/// Testes do <see cref="SimuladorRoteiro"/>.
///
/// Ele é a base dos testes de resiliência que vêm depois, então precisa ser
/// exato: se o roteiro não entregar os passos na ordem prometida, todo teste
/// construído em cima dele mente.
/// </summary>
public class SimuladorRoteiroTests
{
    private static readonly DateTime Agora =
        new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static PassoSimulado Leitura(uint contador) =>
        new PassoSimulado.Leitura(
            LeituraPlc.Zerada(Agora) with { TotalPaletes = contador });

    private static PassoSimulado Falha(string motivo = "PLC fora do ar") =>
        new PassoSimulado.Falha(motivo);

    // ---------------------------------------------------------------
    // Ordem
    // ---------------------------------------------------------------

    [Fact]
    public async Task LerAsync_DevolveOsPassosNaOrdemDoRoteiro()
    {
        await using var simulador = new SimuladorRoteiro(
            [Leitura(10), Leitura(20), Leitura(30)],
            SimuladorRoteiro.AoFimDoRoteiro.Falhar);

        Assert.Equal(10u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(20u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(30u, (await simulador.LerAsync()).TotalPaletes);
    }

    [Fact]
    public async Task LerAsync_ContaOsPassosConsumidos()
    {
        await using var simulador = new SimuladorRoteiro([Leitura(1), Leitura(2)]);

        Assert.Equal(0, simulador.PassosConsumidos);

        await simulador.LerAsync();
        await simulador.LerAsync();

        Assert.Equal(2, simulador.PassosConsumidos);
    }

    // ---------------------------------------------------------------
    // Falha roteirizada
    // ---------------------------------------------------------------

    /// <summary>
    /// A falha precisa chegar como PlcIndisponivelException — a mesma que o
    /// S7DataSource lança. Se viesse outro tipo, testar o coletor contra o
    /// simulador não provaria nada sobre o comportamento real.
    /// </summary>
    [Fact]
    public async Task LerAsync_PassoDeFalha_LancaPlcIndisponivel()
    {
        await using var simulador = new SimuladorRoteiro(
            [Falha("cabo solto")],
            SimuladorRoteiro.AoFimDoRoteiro.RepetirUltimo);

        var erro = await Assert.ThrowsAsync<PlcIndisponivelException>(
            () => simulador.LerAsync());

        Assert.Contains("cabo solto", erro.Message);
    }

    [Fact]
    public async Task LerAsync_FalhaNoMeio_NaoImpedeOsPassosSeguintes()
    {
        await using var simulador = new SimuladorRoteiro(
            [Leitura(1), Falha(), Leitura(3)],
            SimuladorRoteiro.AoFimDoRoteiro.Falhar);

        Assert.Equal(1u, (await simulador.LerAsync()).TotalPaletes);

        await Assert.ThrowsAsync<PlcIndisponivelException>(() => simulador.LerAsync());

        Assert.Equal(3u, (await simulador.LerAsync()).TotalPaletes);
    }

    // ---------------------------------------------------------------
    // Políticas de fim
    // ---------------------------------------------------------------

    [Fact]
    public async Task AoFim_RepetirDoInicio_VoltaAoPrimeiroPasso()
    {
        await using var simulador = new SimuladorRoteiro(
            [Leitura(10), Leitura(20)],
            SimuladorRoteiro.AoFimDoRoteiro.RepetirDoInicio);

        Assert.Equal(10u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(20u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(10u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(20u, (await simulador.LerAsync()).TotalPaletes);
    }

    [Fact]
    public async Task AoFim_RepetirUltimo_CongelaNoEstadoFinal()
    {
        await using var simulador = new SimuladorRoteiro(
            [Leitura(10), Leitura(99)],
            SimuladorRoteiro.AoFimDoRoteiro.RepetirUltimo);

        await simulador.LerAsync();
        await simulador.LerAsync();

        Assert.Equal(99u, (await simulador.LerAsync()).TotalPaletes);
        Assert.Equal(99u, (await simulador.LerAsync()).TotalPaletes);
    }

    /// <summary>
    /// Permite provar que o coletor não pediu mais leituras do que o esperado:
    /// a leitura a mais estoura em vez de passar despercebida.
    /// </summary>
    [Fact]
    public async Task AoFim_Falhar_PassaAFalharDepoisDoUltimoPasso()
    {
        await using var simulador = new SimuladorRoteiro(
            [Leitura(10)],
            SimuladorRoteiro.AoFimDoRoteiro.Falhar);

        await simulador.LerAsync();

        await Assert.ThrowsAsync<PlcIndisponivelException>(() => simulador.LerAsync());
    }

    // ---------------------------------------------------------------
    // Validação e ciclo de vida
    // ---------------------------------------------------------------

    [Fact]
    public void Construtor_RoteiroVazio_Explode()
    {
        Assert.Throws<ArgumentException>(
            () => new SimuladorRoteiro([]));
    }

    [Fact]
    public async Task LerAsync_ComTokenCancelado_Explode()
    {
        await using var simulador = new SimuladorRoteiro([Leitura(1)]);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => simulador.LerAsync(cts.Token));
    }

    [Fact]
    public async Task LerAsync_DepoisDeDescartado_Explode()
    {
        var simulador = new SimuladorRoteiro([Leitura(1)]);
        await simulador.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => simulador.LerAsync());
    }
}