using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Iot.Cenario;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Regras e Z salvos durante a medição valem na hora: o estado troca a configuração sem
/// perder contadores, sensores nem a parada em curso (19/10).
/// </summary>
public class EstadoMaquinaIotRecargaTests
{
    private static EstadoMaquinaIot ParadaSemMotivo(ConfiguracaoColetaIot config)
    {
        var estado = new EstadoMaquinaIot(config);
        for (var s = 0; s <= 60; s += 20)
            estado.Processar(Amostra(s, s2: (uint)(1000 + s * 5)));
        // S8 ativo (falta de garrafas), mas sem regra que o use: a parada nasce sem motivo.
        for (var s = 80; s <= 120; s += 20)
            estado.Processar(Amostra(s, s2: 1300, faltaGarrafas: true));
        Assert.Equal(SituacaoMaquina.Parada, estado.Situacao);
        Assert.True(estado.ClassificacaoAtual!.EhNaoClassificada);
        return estado;
    }

    [Fact]
    public void RegraNova_ClassificaAParadaEmCursoSemMotivo_EMantemOResto()
    {
        var estado = ParadaSemMotivo(Config(regras: []));

        var eventos = estado.AtualizarConfiguracao(Config(regras: RegrasPadrao), Em(130));

        var reclass = Assert.IsType<ParadaReclassificada>(Assert.Single(eventos));
        Assert.Equal(MotivoFaltaGarrafas, reclass.Classificacao.MotivoParadaId);
        Assert.Equal(TipoParada.Externa, estado.ClassificacaoAtual!.Tipo);
        Assert.Equal(Em(60), estado.InicioParadaUtc);                    // a parada não recomeçou
        Assert.Equal(1300u, estado.ContadoresBrutos[CanalWise.S2]);      // contadores preservados
        Assert.True(estado.EstadosSensores[CanalWise.S8]);               // sensores preservados
        Assert.Contains(CanalWise.S8, estado.SensoresEmFalha());         // a cor já segue a regra nova
    }

    [Fact]
    public void ParadaJaClassificada_NaoTrocaDeMotivoComRegraNova()
    {
        var estado = ParadaSemMotivo(Config(regras: []));
        estado.AtualizarConfiguracao(Config(regras: RegrasPadrao), Em(130)); // classificou como falta de garrafas

        // Regra nova dá outro motivo ao mesmo sensor: a primeira causa fica.
        var outra = new Regra(Guid.NewGuid(), 1, [Condicao.EmAlarme(CanalWise.S8)], TipoParada.Interna, MotivoAcumuloMinimo);
        var eventos = estado.AtualizarConfiguracao(Config(regras: [outra]), Em(140));

        Assert.Empty(eventos);
        Assert.Equal(MotivoFaltaGarrafas, estado.ClassificacaoAtual!.MotivoParadaId);
    }

    [Fact]
    public void SemParada_RecargaNaoGeraEvento_EZNovoValeNaProximaVerificacao()
    {
        var estado = new EstadoMaquinaIot(Config());
        for (var s = 0; s <= 60; s += 20)
            estado.Processar(Amostra(s, s2: (uint)(1000 + s * 5)));

        // Z de 60 s → 20 s: a amostra seguinte sem produção já abre a parada.
        var curto = new ConfiguracaoColetaIot(
            [new(CanalWise.S1), new(CanalWise.S2), new(CanalWise.S3), new(CanalWise.S4), new(CanalWise.S7), new(CanalWise.S8)],
            TimeSpan.FromSeconds(20), LimiteComunicacao, RegrasPadrao);
        Assert.Empty(estado.AtualizarConfiguracao(curto, Em(70)));

        var eventos = estado.Processar(Amostra(80, s2: 1300));
        Assert.IsType<ParadaIniciada>(Assert.Single(eventos));
    }

    [Fact]
    public void InstanteForaDeUtc_Falha()
    {
        var estado = new EstadoMaquinaIot(Config());
        Assert.Throws<ArgumentException>(() => estado.AtualizarConfiguracao(Config(), new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Local)));
    }
}
