using SmartLine.Iot.Simulacao;

namespace SmartLine.Tests.Iot;

/// <summary>Paradas automáticas do simulador de WISE.</summary>
public class ParadasAleatoriasTests
{
    private static readonly DateTime T0 = new(2026, 9, 30, 13, 0, 0, DateTimeKind.Utc);

    private static readonly OpcoesParadasAleatorias Opcoes =
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

    private static MaquinaSimulada Maquina(int numero = 1) => new(numero, 600, 1, T0);

    [Fact]
    public void ForaDoAutomatico_NaoMudaNada()
    {
        var auto = new ParadasAleatorias(Opcoes, new Random(1));
        var m = Maquina();

        Assert.Null(auto.Verificar(m, T0.AddHours(5)));
        Assert.Equal(CenarioSimulado.Rodando, m.Cenario);
        Assert.False(auto.Ligado(m));
    }

    [Fact]
    public void Ciclo_RodaEntre50E150PorCentoDoMedio_ParaDentroDaFaixa_EVolta()
    {
        var auto = new ParadasAleatorias(Opcoes, new Random(7));
        var m = Maquina();
        auto.Ligar(m, T0);
        var agora = T0;

        for (var i = 0; i < 200; i++)
        {
            // Rodando até a próxima mudança
            var parar = auto.ProximaMudanca(m)!.Value;
            Assert.InRange(parar - agora, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));
            Assert.Null(auto.Verificar(m, parar.AddSeconds(-1)));
            var parada = auto.Verificar(m, parar);
            Assert.NotNull(parada);
            Assert.NotEqual(CenarioSimulado.Rodando, parada);

            // Parado até voltar
            var voltar = auto.ProximaMudanca(m)!.Value;
            Assert.InRange(voltar - parar, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            Assert.Equal(CenarioSimulado.Rodando, auto.Verificar(m, voltar));
            agora = voltar;
        }
    }

    [Fact]
    public void Sorteia_TodosOsTiposDeParada_ESemDesligadoQuandoPedido()
    {
        var com = Sortear(new ParadasAleatorias(Opcoes, new Random(3)));
        var sem = Sortear(new ParadasAleatorias(Opcoes with { IncluirDesligado = false }, new Random(3)));

        Assert.Equal(
            new[]
            {
                CenarioSimulado.FaltaGarrafas, CenarioSimulado.AbaixoAcumuloMinimo, CenarioSimulado.SaidaGarrafasBloqueada,
                CenarioSimulado.SaidaCaixasBloqueada, CenarioSimulado.ParadaSemCausa, CenarioSimulado.Desligado
            }.Order(),
            com.Order());
        Assert.DoesNotContain(CenarioSimulado.Desligado, sem);
        Assert.Equal(5, sem.Count);
    }

    [Fact]
    public void Desligar_ParaOAutomatico_EAMaquinaFicaComoEsta_LigarRetomaDoCenarioAtual()
    {
        var auto = new ParadasAleatorias(Opcoes, new Random(5));
        var m = Maquina();
        auto.Ligar(m, T0);
        auto.Verificar(m, auto.ProximaMudanca(m)!.Value); // parou
        var cenario = m.Cenario;

        auto.Desligar(m);
        Assert.Null(auto.Verificar(m, T0.AddDays(1)));
        Assert.Equal(cenario, m.Cenario);

        auto.Ligar(m, T0.AddDays(1));
        Assert.InRange(auto.ProximaMudanca(m)!.Value - T0.AddDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)); // parado: tempo de parada
    }

    [Fact]
    public void CadaMaquina_TemOProprioRelogio()
    {
        var auto = new ParadasAleatorias(Opcoes, new Random(11));
        var m1 = Maquina(1);
        var m2 = Maquina(2);
        auto.Ligar(m1, T0);
        auto.Ligar(m2, T0.AddMinutes(30));

        auto.Verificar(m1, auto.ProximaMudanca(m1)!.Value);

        Assert.NotEqual(CenarioSimulado.Rodando, m1.Cenario);
        Assert.Equal(CenarioSimulado.Rodando, m2.Cenario);
        Assert.True(auto.ProximaMudanca(m2) >= T0.AddMinutes(35));
    }

    [Theory]
    [InlineData(0, 1, 5)]
    [InlineData(10, 0, 5)]
    [InlineData(10, 5, 1)]
    public void OpcoesInvalidas_Recusa(int rodandoMin, int paradaMin, int paradaMax)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParadasAleatorias(
            new OpcoesParadasAleatorias(TimeSpan.FromMinutes(rodandoMin), TimeSpan.FromMinutes(paradaMin), TimeSpan.FromMinutes(paradaMax))));
    }

    // Tipos de parada que saem em 300 ciclos
    private static HashSet<CenarioSimulado> Sortear(ParadasAleatorias auto)
    {
        var m = Maquina();
        auto.Ligar(m, T0);
        var vistos = new HashSet<CenarioSimulado>();
        for (var i = 0; i < 300; i++)
        {
            var parada = auto.Verificar(m, auto.ProximaMudanca(m)!.Value)!.Value;
            vistos.Add(parada);
            auto.Verificar(m, auto.ProximaMudanca(m)!.Value);
        }
        return vistos;
    }
}
