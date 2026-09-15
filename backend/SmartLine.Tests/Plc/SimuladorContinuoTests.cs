using Microsoft.Extensions.Time.Testing;
using SmartLine.Core.Plc;
using SmartLine.Plc.Simulacao;

namespace SmartLine.Tests.Plc;

/// <summary>
/// Testes do <see cref="SimuladorContinuo"/>.
///
/// Usam <see cref="FakeTimeProvider"/>: horas de produção simulada rodam em
/// milissegundos, sem espera real.
/// </summary>
public class SimuladorContinuoTests
{
    private static readonly DateTimeOffset Inicio =
        new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider NovoRelogio() => new(Inicio);

    /// <summary>
    /// Perfil sem paradas na prática: o tempo médio entre paradas é tão longo
    /// que nenhuma acontece na janela testada. Isola a aritmética de produção.
    /// </summary>
    private static OpcoesSimuladorContinuo SemParadas(double paletesPorHora = 20) => new()
    {
        PaletesPorHora = paletesPorHora,
        TempoMedioEntreParadas = TimeSpan.FromDays(365),
        Semente = 12345
    };

    // ---------------------------------------------------------------
    // Produção
    // ---------------------------------------------------------------

    [Fact]
    public async Task LerAsync_PrimeiraLeitura_ComecaOperandoESemProducao()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(), relogio);

        var leitura = await simulador.LerAsync();

        Assert.Equal((uint)EstadoWs.Operating, leitura.CodigoEstado);
        Assert.Equal(0u, leitura.TotalPaletes);
    }

    [Fact]
    public async Task LerAsync_AposUmaHoraOperando_ProduzORitmoNominal()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(20), relogio);

        await simulador.LerAsync();
        relogio.Advance(TimeSpan.FromHours(1));

        var leitura = await simulador.LerAsync();

        Assert.Equal(EstadoWs.Operating, simulador.EstadoAtual);
        Assert.InRange(leitura.TotalPaletes, 19u, 20u);
    }

    [Fact]
    public async Task LerAsync_AposOitoHoras_ProduzProporcionalAoRitmo()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(20), relogio);

        await simulador.LerAsync();
        relogio.Advance(TimeSpan.FromHours(8));

        var leitura = await simulador.LerAsync();

        Assert.InRange(leitura.TotalPaletes, 159u, 160u);
    }

    /// <summary>
    /// A simulação é dirigida pelo relógio, não pelo número de chamadas.
    /// Ler de segundo em segundo ou de minuto em minuto tem que dar a mesma
    /// produção — se não desse, polling mais rápido "fabricaria" paletes.
    /// </summary>
    [Fact]
    public async Task LerAsync_IntervaloDePollingNaoAlteraAProducao()
    {
        var contadorFino = await ProduzirEmUmaHora(TimeSpan.FromSeconds(1));
        var contadorGrosso = await ProduzirEmUmaHora(TimeSpan.FromMinutes(5));

        Assert.Equal(contadorFino, contadorGrosso);

        static async Task<uint> ProduzirEmUmaHora(TimeSpan passo)
        {
            var relogio = NovoRelogio();
            await using var simulador = new SimuladorContinuo(
                new OpcoesSimuladorContinuo
                {
                    PaletesPorHora = 20,
                    TempoMedioEntreParadas = TimeSpan.FromMinutes(10),
                    Semente = 777
                },
                relogio);

            // Inicializa em t=0 nos dois casos: o simulador arranca na primeira
            // leitura, então começar em instantes diferentes já divergiria.
            var ultima = await simulador.LerAsync();

            // Avança e só então lê, para que a última leitura de ambos caia
            // exatamente em t=1h. Lendo antes de avançar, o passo grosso pararia
            // em 55 min e o fino em 59 min 59 s — instantes diferentes.
            var decorrido = TimeSpan.Zero;

            while (decorrido < TimeSpan.FromHours(1))
            {
                relogio.Advance(passo);
                decorrido += passo;
                ultima = await simulador.LerAsync();
            }

            return ultima.TotalPaletes;
        }
    }

    [Fact]
    public async Task LerAsync_RelogioParado_NaoProduz()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(), relogio);

        await simulador.LerAsync();
        relogio.Advance(TimeSpan.FromHours(1));

        var primeira = await simulador.LerAsync();
        var segunda = await simulador.LerAsync();

        Assert.Equal(primeira.TotalPaletes, segunda.TotalPaletes);
    }

    // ---------------------------------------------------------------
    // Determinismo
    // ---------------------------------------------------------------

    [Fact]
    public async Task LerAsync_MesmaSemente_ProduzOMesmoResultado()
    {
        var a = await RodarDozeHoras(semente: 42);
        var b = await RodarDozeHoras(semente: 42);

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task LerAsync_SementesDiferentes_ProduzemResultadosDiferentes()
    {
        var a = await RodarDozeHoras(semente: 1);
        var b = await RodarDozeHoras(semente: 2);

        Assert.NotEqual(a, b);
    }

    private static async Task<uint> RodarDozeHoras(int semente)
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(
            new OpcoesSimuladorContinuo { Semente = semente },
            relogio);

        await simulador.LerAsync();
        relogio.Advance(TimeSpan.FromHours(12));

        return (await simulador.LerAsync()).TotalPaletes;
    }

    // ---------------------------------------------------------------
    // Paradas
    // ---------------------------------------------------------------

    /// <summary>
    /// Com o perfil padrão, um dia de simulação tem que gerar paradas dos dois
    /// grupos: falta/acúmulo (externas) e falha de equipamento (interna).
    /// É o que o classificador vai precisar exercitar.
    /// </summary>
    [Fact]
    public async Task LerAsync_AoLongoDeUmDia_GeraParadasDeVariosTipos()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(
            new OpcoesSimuladorContinuo { Semente = 2024 },
            relogio);

        var estadosVistos = new HashSet<EstadoWs>();

        for (var i = 0; i < 24 * 60 * 2; i++) // 24 h em passos de 30 s
        {
            var leitura = await simulador.LerAsync();
            estadosVistos.Add((EstadoWs)leitura.CodigoEstado);
            relogio.Advance(TimeSpan.FromSeconds(30));
        }

        Assert.Contains(EstadoWs.Operating, estadosVistos);
        Assert.True(
            estadosVistos.Count > 1,
            "Um dia inteiro sem nenhuma parada indica que o sorteio não está funcionando.");
    }

    [Fact]
    public async Task LerAsync_DuranteParadaComAlarme_ReportaOCodigoDeFalha()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(
            new OpcoesSimuladorContinuo
            {
                TempoMedioEntreParadas = TimeSpan.FromSeconds(30),
                Paradas =
                [
                    new ParadaSimulada(
                        EstadoWs.EquipmentFailure,
                        Peso: 1,
                        DuracaoMedia: TimeSpan.FromMinutes(10),
                        CodigoFalha: 4201)
                ],
                Semente = 99
            },
            relogio);

        await simulador.LerAsync();

        LeituraPlc? emParada = null;

        for (var i = 0; i < 200 && emParada is null; i++)
        {
            relogio.Advance(TimeSpan.FromSeconds(10));
            var leitura = await simulador.LerAsync();

            if (leitura.CodigoEstado == (uint)EstadoWs.EquipmentFailure)
                emParada = leitura;
        }

        Assert.NotNull(emParada);
        Assert.Equal(4201u, emParada.CodigoFalha);
    }

    [Fact]
    public async Task LerAsync_EmParada_NaoAcumulaProducao()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(
            new OpcoesSimuladorContinuo
            {
                PaletesPorHora = 1000,
                TempoMedioEntreParadas = TimeSpan.FromSeconds(1),
                Paradas =
                [
                    new ParadaSimulada(EstadoWs.Lack, Peso: 1, DuracaoMedia: TimeSpan.FromHours(10))
                ],
                Semente = 5
            },
            relogio);

        await simulador.LerAsync();
        relogio.Advance(TimeSpan.FromSeconds(30));
        var aoEntrarEmParada = await simulador.LerAsync();

        Assert.Equal((uint)EstadoWs.Lack, aoEntrarEmParada.CodigoEstado);

        relogio.Advance(TimeSpan.FromHours(1));
        var depois = await simulador.LerAsync();

        Assert.Equal(aoEntrarEmParada.TotalPaletes, depois.TotalPaletes);
    }

    // ---------------------------------------------------------------
    // Rollover
    // ---------------------------------------------------------------

    /// <summary>
    /// Começando perto do topo do uint, o contador vira. É assim que o
    /// tratamento de rollover do coletor vai ser exercitado sem esperar anos.
    /// </summary>
    [Fact]
    public async Task LerAsync_ContadorPertoDoTopo_ViraNaturalmente()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(
            SemParadas(20) with { ContadorInicial = uint.MaxValue - 5 },
            relogio);

        var antes = await simulador.LerAsync();
        Assert.True(antes.TotalPaletes > uint.MaxValue - 10);

        relogio.Advance(TimeSpan.FromHours(2));
        var depois = await simulador.LerAsync();

        Assert.True(
            depois.TotalPaletes < 1000,
            $"Esperava o contador virado, veio {depois.TotalPaletes}.");
    }

    // ---------------------------------------------------------------
    // Contrato
    // ---------------------------------------------------------------

    [Fact]
    public async Task LerAsync_TimestampVemEmUtc()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(), relogio);

        var leitura = await simulador.LerAsync();

        Assert.Equal(DateTimeKind.Utc, leitura.TimestampUtc.Kind);
    }

    [Fact]
    public async Task LerAsync_ReportaVelocidadeProjetadaDerivadaDoRitmo()
    {
        var relogio = NovoRelogio();
        await using var simulador = new SimuladorContinuo(SemParadas(60), relogio);

        var leitura = await simulador.LerAsync();

        // 60 paletes/hora = 1 por minuto.
        Assert.Equal(1f, leitura.VelocidadeProjetada, precision: 4);
    }

    [Fact]
    public async Task LerAsync_DepoisDeDescartado_Explode()
    {
        var simulador = new SimuladorContinuo(SemParadas(), NovoRelogio());
        await simulador.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => simulador.LerAsync());
    }

    // ---------------------------------------------------------------
    // Validação das opções
    // ---------------------------------------------------------------

    [Fact]
    public void Construtor_RitmoZerado_Explode()
    {
        Assert.Throws<ArgumentException>(
            () => new SimuladorContinuo(new OpcoesSimuladorContinuo { PaletesPorHora = 0 }));
    }

    [Fact]
    public void Construtor_SemTipoDeParada_Explode()
    {
        Assert.Throws<ArgumentException>(
            () => new SimuladorContinuo(new OpcoesSimuladorContinuo { Paradas = [] }));
    }

    [Fact]
    public void Construtor_ComOperatingComoParada_Explode()
    {
        Assert.Throws<ArgumentException>(
            () => new SimuladorContinuo(new OpcoesSimuladorContinuo
            {
                Paradas = [new ParadaSimulada(EstadoWs.Operating, 1, TimeSpan.FromMinutes(1))]
            }));
    }
}