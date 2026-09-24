using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Iot.Cenario;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Cenários da coleta por WISE. Convenção: o WISE publica a cada 20 s,
/// Z = 60 s e o limite de comunicação é 90 s (ver <see cref="Cenario"/>).
/// </summary>
public class EstadoMaquinaIotTests
{
    /// <summary>
    /// Máquina rodando de 0 a <paramref name="ateSegundos"/>, S2 subindo 100
    /// por amostra. Com o padrão, termina em S2 = 1300 aos 60 s.
    /// </summary>
    private static EstadoMaquinaIot Rodando(int ateSegundos = 60, ConfiguracaoColetaIot? config = null)
    {
        var estado = new EstadoMaquinaIot(config ?? Config());
        for (var s = 0; s <= ateSegundos; s += 20)
            estado.Processar(Amostra(s, s2: (uint)(1000 + s * 5)));
        return estado;
    }

    /// <summary>Amostras periódicas sem incremento de produção, de <paramref name="de"/> a <paramref name="ate"/>.</summary>
    private static void SemProduzir(EstadoMaquinaIot estado, int de, int ate, uint s2 = 1300, bool faltaGarrafas = false)
    {
        for (var s = de; s <= ate; s += 20)
            estado.Processar(Amostra(s, s2: s2, faltaGarrafas: faltaGarrafas));
    }

    // ── Produção ──────────────────────────────────────────────────

    [Fact]
    public void PrimeiraAmostra_SoViraReferencia()
    {
        var estado = new EstadoMaquinaIot(Config());

        var eventos = estado.Processar(Amostra(0, s2: 5000, s3: 10));

        Assert.Empty(eventos);
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void Incremento_ViraGarrafasPeloMultiplicador()
    {
        // Sensor contando caixas de 12.
        var estado = new EstadoMaquinaIot(Config([new(CanalWise.S2, 12), new(CanalWise.S3)]));
        estado.Processar(Amostra(0, s2: 100, s3: 0));

        var eventos = estado.Processar(Amostra(20, s2: 110, s3: 2));

        var producao = Assert.IsType<ProducaoApurada>(Assert.Single(eventos));
        Assert.Equal(120, producao.Garrafas);
        Assert.Equal(2, producao.Rejeito);
        Assert.Equal(Em(20), producao.InstanteUtc);
    }

    [Fact]
    public void EntradaTripla_SomaOsCanaisMarcados_EIgnoraOsNaoMarcados()
    {
        // PAL tripla: S2 e S5 contam caixas de 6, S6 conta garrafas. S3 não marcado.
        var estado = new EstadoMaquinaIot(Config([new(CanalWise.S2, 6), new(CanalWise.S5, 6), new(CanalWise.S6, 1)]));
        estado.Processar(Amostra(0, s2: 0, s3: 0, s5: 0, s6: 0));

        var eventos = estado.Processar(Amostra(20, s2: 10, s3: 50, s5: 5, s6: 7));

        var producao = Assert.IsType<ProducaoApurada>(Assert.Single(eventos));
        Assert.Equal(10 * 6 + 5 * 6 + 7, producao.Garrafas);
        Assert.Equal(0, producao.Rejeito);
    }

    [Fact]
    public void ContadorVoltouParaTras_DescartaIntervalo_SemProducaoNegativa()
    {
        var estado = new EstadoMaquinaIot(Config());
        estado.Processar(Amostra(0, s2: 90_000));

        var reinicio = estado.Processar(Amostra(20, s2: 30));
        var depois = estado.Processar(Amostra(40, s2: 80));

        var evento = Assert.IsType<ContadorReiniciado>(Assert.Single(reinicio));
        Assert.Equal("S2", evento.Contador);
        Assert.Equal(90_000, evento.ValorAnterior);
        Assert.Equal(30, evento.ValorNovo);
        Assert.Equal(50, Assert.IsType<ProducaoApurada>(Assert.Single(depois)).Garrafas);
    }

    [Fact]
    public void ContadoresRestaurados_PrimeiraAmostraCreditaOQueFoiProduzidoComBackendFora()
    {
        var estado = new EstadoMaquinaIot(Config(), new Dictionary<CanalWise, uint> { [CanalWise.S2] = 1000 });

        var eventos = estado.Processar(Amostra(0, s2: 1600));

        Assert.Equal(600, Assert.IsType<ProducaoApurada>(Assert.Single(eventos)).Garrafas);
    }

    [Fact]
    public void AmostraForaDeOrdem_EhIgnorada()
    {
        var estado = new EstadoMaquinaIot(Config());
        estado.Processar(Amostra(0, s2: 0));
        estado.Processar(Amostra(40, s2: 200));

        Assert.Empty(estado.Processar(Amostra(20, s2: 100)));
        Assert.Equal(200u, estado.ContadoresBrutos[CanalWise.S2]);
    }

    [Fact]
    public void TimestampForaDeUtc_Falha()
    {
        var estado = new EstadoMaquinaIot(Config());
        var local = new AmostraWise { TimestampUtc = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Local) };

        Assert.Throws<ArgumentException>(() => estado.Processar(local));
        Assert.Throws<ArgumentException>(() => estado.Verificar(DateTime.Now));
    }

    // ── Detecção de parada ────────────────────────────────────────

    [Fact]
    public void SemIncrementoAntesDeZ_ContinuaRodando()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 100); // 40 s sem incremento

        Assert.Empty(estado.Verificar(Em(119)));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void AmostraSemIncrementoHaZ_IniciaParada_NoInstanteDoUltimoIncremento()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 100);

        var eventos = estado.Processar(Amostra(120, s2: 1300));

        var parada = Assert.IsType<ParadaIniciada>(Assert.Single(eventos));
        Assert.Equal(Em(60), parada.InstanteUtc);
        Assert.Equal(SituacaoMaquina.Parada, estado.Situacao);
        Assert.Equal(Em(60), estado.InicioParadaUtc);
    }

    [Fact]
    public void SemAmostraNova_VerificarNaoAbreParada()
    {
        // WISE em silêncio: passou de Z, mas não há evidência de que a máquina parou.
        var estado = Rodando();

        Assert.Empty(estado.Verificar(Em(89)));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void SoRejeitoSubindo_NaoContaComoMaquinaRodando()
    {
        var estado = new EstadoMaquinaIot(Config());
        estado.Processar(Amostra(0, s2: 100, s3: 0));
        estado.Processar(Amostra(40, s2: 100, s3: 5));

        var eventos = estado.Processar(Amostra(60, s2: 100, s3: 9));

        Assert.Contains(eventos, e => e is ProducaoApurada { Garrafas: 0, Rejeito: 4 });
        Assert.Contains(eventos, e => e is ParadaIniciada p && p.InstanteUtc == Em(0));
    }

    [Fact]
    public void VoltaAProduzir_EncerraParada_NoInstanteDaAmostra()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 280); // parada confirmada aos 120 s

        var eventos = estado.Processar(Amostra(300, s2: 1400));

        Assert.Collection(eventos,
            e => Assert.Equal(100, Assert.IsType<ProducaoApurada>(e).Garrafas),
            e => Assert.Equal(Em(300), Assert.IsType<ParadaEncerrada>(e).InstanteUtc));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
        Assert.Null(estado.ClassificacaoAtual);
    }

    [Fact]
    public void DepoisDeVoltar_NovaParadaUsaONovoUltimoIncremento()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 280);
        estado.Processar(Amostra(300, s2: 1400));
        SemProduzir(estado, 320, 340, s2: 1400);

        var eventos = estado.Processar(Amostra(360, s2: 1400));

        Assert.Equal(Em(300), Assert.IsType<ParadaIniciada>(Assert.Single(eventos)).InstanteUtc);
    }

    // ── Classificação ─────────────────────────────────────────────

    [Fact]
    public void ParadaSemSensorEmAlarme_NasceNaoClassificada()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 100);

        var parada = Assert.IsType<ParadaIniciada>(Assert.Single(estado.Processar(Amostra(120, s2: 1300))));

        Assert.True(parada.Classificacao.EhNaoClassificada);
        Assert.Equal(TipoParada.Interna, parada.Classificacao.Tipo);
    }

    [Fact]
    public void ParadaComFaltaDeGarrafas_NasceExterna()
    {
        var estado = Rodando();
        estado.Processar(Amostra(70, faltaGarrafas: true)); // C.O.S. do S8, sem contadores
        SemProduzir(estado, 80, 100, faltaGarrafas: true);

        var eventos = estado.Processar(Amostra(120, s2: 1300, faltaGarrafas: true));

        var parada = Assert.IsType<ParadaIniciada>(Assert.Single(eventos));
        Assert.Equal(TipoParada.Externa, parada.Classificacao.Tipo);
        Assert.Equal(MotivoFaltaGarrafas, parada.Classificacao.MotivoParadaId);
    }

    [Fact]
    public void SensorEmAlarmeComMaquinaRodando_NaoGeraEvento()
    {
        var estado = Rodando();

        var eventos = estado.Processar(Amostra(80, s2: 1500, saidaGarrafasBloqueada: true));

        Assert.IsType<ProducaoApurada>(Assert.Single(eventos));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void CausaMudaDuranteAParada_Reclassifica()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 180, faltaGarrafas: true); // parada externa desde 60 s

        // Falta resolvida, mas a máquina segue parada.
        var eventos = estado.Processar(Amostra(200, s2: 1300, faltaGarrafas: false));

        var reclass = Assert.IsType<ParadaReclassificada>(Assert.Single(eventos));
        Assert.Equal(Em(200), reclass.InstanteUtc);
        Assert.True(reclass.Classificacao.EhNaoClassificada);
        Assert.Same(ClassificacaoParada.NaoClassificada, estado.ClassificacaoAtual);
    }

    [Fact]
    public void MensagemRepetidaDuranteAParada_NaoReclassifica()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 120, faltaGarrafas: true);

        Assert.Empty(estado.Processar(Amostra(140, s2: 1300, faltaGarrafas: true)));
        Assert.Empty(estado.Verificar(Em(150)));
    }

    [Fact]
    public void SensorNaoMarcadoNaMedicao_NaoClassifica()
    {
        // Só S2 marcado: o S8 chega em alarme, mas foi desmarcado na medição.
        var estado = Rodando(config: Config([new(CanalWise.S2)]));
        SemProduzir(estado, 80, 100, faltaGarrafas: true);

        var eventos = estado.Processar(Amostra(120, s2: 1300, faltaGarrafas: true));

        Assert.True(Assert.IsType<ParadaIniciada>(Assert.Single(eventos)).Classificacao.EhNaoClassificada);
        Assert.Empty(estado.EstadosSensores);
    }

    [Fact]
    public void RegraPorTempo_ReclassificaQuandoAParadaPassaDoLimite()
    {
        var motivoLonga = Guid.NewGuid();
        var regras = new List<Regra>(RegrasPadrao)
        {
            new(Guid.NewGuid(), 10, [Condicao.ParadaHaPeloMenos(TimeSpan.FromMinutes(15))], TipoParada.Interna, motivoLonga)
        };
        var estado = Rodando(config: Config(regras: regras));
        SemProduzir(estado, 80, 940); // parada desde 60 s

        Assert.Empty(estado.Verificar(Em(60 + 15 * 60 - 1)));
        var reclass = Assert.IsType<ParadaReclassificada>(Assert.Single(estado.Verificar(Em(60 + 15 * 60))));
        Assert.Equal(motivoLonga, reclass.Classificacao.MotivoParadaId);
    }

    // ── Comunicação ───────────────────────────────────────────────

    [Fact]
    public void SemMensagensAlemDoLimite_PerdeComunicacao_SemAbrirParada()
    {
        var estado = Rodando(); // última mensagem aos 60 s

        Assert.Empty(estado.Verificar(Em(60 + 90)));
        var eventos = estado.Verificar(Em(60 + 91));

        var perdida = Assert.IsType<ComunicacaoPerdida>(Assert.Single(eventos));
        Assert.Equal(Em(60), perdida.InstanteUtc);
        Assert.Equal(SituacaoMaquina.SemComunicacao, estado.Situacao);
    }

    [Fact]
    public void PerdaDeComunicacaoComMaquinaParada_FechaAParadaNaUltimaMensagem()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 120); // parada desde 60 s, última mensagem aos 120 s

        var eventos = estado.Verificar(Em(120 + 91));

        Assert.Collection(eventos,
            e => Assert.Equal(Em(120), Assert.IsType<ParadaEncerrada>(e).InstanteUtc),
            e => Assert.Equal(Em(120), Assert.IsType<ComunicacaoPerdida>(e).InstanteUtc));
        Assert.Null(estado.InicioParadaUtc);
    }

    [Fact]
    public void SemComunicacao_NadaMaisAconteceAteVoltar()
    {
        var estado = Rodando();
        estado.Verificar(Em(200));

        Assert.Empty(estado.Verificar(Em(3600)));
        Assert.Equal(SituacaoMaquina.SemComunicacao, estado.Situacao);
    }

    [Fact]
    public void ComunicacaoVolta_CreditaProducaoDoIntervalo_ERecomecaRodando()
    {
        var estado = Rodando();
        estado.Verificar(Em(200));

        var eventos = estado.Processar(Amostra(600, s2: 3300));

        Assert.Collection(eventos,
            e => Assert.Equal(Em(600), Assert.IsType<ComunicacaoRestabelecida>(e).InstanteUtc),
            e => Assert.Equal(2000, Assert.IsType<ProducaoApurada>(e).Garrafas));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void AmostraDepoisDeLongoSilencio_SemVerificarNoMeio_TambemRegistraAPerda()
    {
        var estado = Rodando();

        var eventos = estado.Processar(Amostra(600, s2: 1300));

        Assert.Collection(eventos,
            e => Assert.Equal(Em(60), Assert.IsType<ComunicacaoPerdida>(e).InstanteUtc),
            e => Assert.Equal(Em(600), Assert.IsType<ComunicacaoRestabelecida>(e).InstanteUtc));
    }

    // ── Encerramento ──────────────────────────────────────────────

    [Fact]
    public void Encerrar_ComParadaAberta_FechaNoInstanteInformado()
    {
        var estado = Rodando();
        SemProduzir(estado, 80, 120);

        var eventos = estado.Encerrar(Em(150));

        Assert.Equal(Em(150), Assert.IsType<ParadaEncerrada>(Assert.Single(eventos)).InstanteUtc);
    }

    [Fact]
    public void Encerrar_Rodando_NaoGeraEvento()
    {
        Assert.Empty(Rodando().Encerrar(Em(70)));
    }

    // ── Configuração ──────────────────────────────────────────────

    [Fact]
    public void Configuracao_SemContadorDeProducao_EhRejeitada()
    {
        Assert.Throws<ArgumentException>(() => Config([new(CanalWise.S3), new(CanalWise.S8)]));
    }

    [Fact]
    public void Configuracao_MultiplicadorZero_EhRejeitada()
    {
        Assert.Throws<ArgumentException>(() => Config([new(CanalWise.S2, 0)]));
    }

    [Fact]
    public void Configuracao_CanalRepetido_EhRejeitada()
    {
        Assert.Throws<ArgumentException>(() => Config([new(CanalWise.S2), new(CanalWise.S2, 12)]));
    }
}
