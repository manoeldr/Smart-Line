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
    public void Sorteia_TodosOsTiposDeParada_SemDesligado()
    {
        // A falta de comunicação não é mais uma parada: tem o relógio próprio (ver abaixo).
        var vistos = Sortear(new ParadasAleatorias(Opcoes, new Random(3)));

        Assert.Equal(
            new[]
            {
                CenarioSimulado.FaltaGarrafas, CenarioSimulado.AbaixoAcumuloMinimo, CenarioSimulado.SaidaGarrafasBloqueada,
                CenarioSimulado.SaidaCaixasBloqueada, CenarioSimulado.ParadaSemCausa
            }.Order(),
            vistos.Order());
    }

    // ── Quedas de comunicação ─────────────────────────────────────

    [Fact]
    public void QuedasDeComunicacao_EspacadasELongas_AlternandoComAVolta_DosDoisTipos()
    {
        var auto = new ParadasAleatorias(Opcoes, new Random(9));
        var m = Maquina();
        auto.Ligar(m, T0);
        var agora = T0;
        var tipos = new HashSet<FalhaComunicacao>();

        for (var i = 0; i < 100; i++)
        {
            // Comunicando: 30 a 90 min (50% a 150% dos 60 min padrão) até cair
            var cair = auto.ProximaMudancaComunicacao(m)!.Value;
            Assert.InRange(cair - agora, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90));
            Assert.Null(auto.VerificarComunicacao(m, cair.AddSeconds(-1)));
            var queda = auto.VerificarComunicacao(m, cair)!;
            Assert.NotNull(queda.Falha);
            Assert.False(m.Publica);
            tipos.Add(queda.Falha!.Value);

            // Fora: de 3 a 10 min
            Assert.InRange(queda.Ate - cair, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10));
            var volta = auto.VerificarComunicacao(m, queda.Ate)!;
            Assert.Null(volta.Falha);
            Assert.True(m.Publica);
            agora = queda.Ate;
        }

        Assert.Equal(new[] { FalhaComunicacao.SemRede, FalhaComunicacao.SemEnergia }.Order(), tipos.Order());
    }

    [Fact]
    public void SemRede_ContinuaContando_SemEnergia_NaoContaEVoltaZerado()
    {
        var semRede = Maquina(1);
        semRede.PerderComunicacao(FalhaComunicacao.SemRede, T0);
        semRede.RecuperarComunicacao(T0.AddMinutes(5));
        Assert.Equal(3000u, semRede.ContadorProducao); // 600/min durante a queda

        var semEnergia = Maquina(2);
        semEnergia.Avancar(T0.AddMinutes(1)); // 600 antes de cair
        semEnergia.PerderComunicacao(FalhaComunicacao.SemEnergia, T0.AddMinutes(1));
        Assert.Equal(600u, semEnergia.ContadorProducao);
        semEnergia.RecuperarComunicacao(T0.AddMinutes(6));
        Assert.Equal(0u, semEnergia.ContadorProducao); // reiniciou
    }

    [Fact]
    public void SemQuedasQuandoPedido_EDesligarOAutomaticoDevolveAComunicacao()
    {
        var sem = new ParadasAleatorias(Opcoes with { IncluirDesligado = false }, new Random(1));
        var m1 = Maquina(1);
        sem.Ligar(m1, T0);
        Assert.Null(sem.ProximaMudancaComunicacao(m1));
        Assert.Null(sem.VerificarComunicacao(m1, T0.AddDays(1)));

        var com = new ParadasAleatorias(Opcoes, new Random(1));
        var m2 = Maquina(2);
        com.Ligar(m2, T0);
        com.VerificarComunicacao(m2, com.ProximaMudancaComunicacao(m2)!.Value);
        Assert.False(m2.Publica);

        com.Desligar(m2, T0.AddHours(3));
        Assert.True(m2.Publica);
        Assert.Null(com.ProximaMudancaComunicacao(m2));
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
