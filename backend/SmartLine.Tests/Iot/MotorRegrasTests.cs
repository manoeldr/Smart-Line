using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Iot.Cenario;

namespace SmartLine.Tests.Iot;

public class MotorRegrasTests
{
    private static ContextoParada Contexto(TimeSpan? duracao = null, params (CanalWise canal, bool bruto)[] estados) =>
        new(estados.ToDictionary(e => e.canal, e => e.bruto), duracao ?? TimeSpan.FromMinutes(1));

    private static readonly IReadOnlyList<Regra> Padrao = MotorRegras.Preparar(RegrasPadrao);

    [Fact]
    public void SemNenhumSensorEmAlarme_NaoClassificada()
    {
        var c = MotorRegras.Classificar(Padrao, Contexto(null, (CanalWise.S8, false), (CanalWise.S7, true)));
        Assert.Same(ClassificacaoParada.NaoClassificada, c);
        Assert.True(c.EhNaoClassificada);
        Assert.Equal(TipoParada.Interna, c.Tipo);
    }

    [Fact]
    public void FaltaDeGarrafas_ClassificaExterna()
    {
        var c = MotorRegras.Classificar(Padrao, Contexto(null, (CanalWise.S8, true)));
        Assert.Equal(TipoParada.Externa, c.Tipo);
        Assert.Equal(MotivoFaltaGarrafas, c.MotivoParadaId);
        Assert.Equal(RegrasPadrao[0].Id, c.RegraId);
    }

    [Fact]
    public void DoisSensoresEmAlarme_VencePrioridadeMenor_IndependenteDaOrdemDaLista()
    {
        var invertida = MotorRegras.Preparar(RegrasPadrao.Reverse());
        // S7 = 0 → saída de garrafas bloqueada; S8 = 1 → falta na entrada.
        var c = MotorRegras.Classificar(invertida, Contexto(null, (CanalWise.S7, false), (CanalWise.S8, true)));
        Assert.Equal(MotivoFaltaGarrafas, c.MotivoParadaId);
    }

    [Fact]
    public void SensorAusenteNoContexto_CondicaoFalsaNosDoisSentidos()
    {
        var motivo = Guid.NewGuid();
        var regras = MotorRegras.Preparar(
        [
            new Regra(Guid.NewGuid(), 1, [Condicao.ForaDeAlarme(CanalWise.S8)], TipoParada.Interna, motivo)
        ]);

        // S8 não lido: não dá para afirmar que está fora de alarme.
        Assert.True(MotorRegras.Classificar(regras, Contexto()).EhNaoClassificada);
        Assert.Equal(motivo, MotorRegras.Classificar(regras, Contexto(null, (CanalWise.S8, false))).MotivoParadaId);
    }

    [Fact]
    public void CondicoesSaoCombinadasComE()
    {
        var motivo = Guid.NewGuid();
        var regras = MotorRegras.Preparar(
        [
            new Regra(Guid.NewGuid(), 1,
                [Condicao.EmAlarme(CanalWise.S8), Condicao.ParadaHaPeloMenos(TimeSpan.FromMinutes(10))],
                TipoParada.Externa, motivo)
        ]);

        Assert.True(MotorRegras.Classificar(regras, Contexto(TimeSpan.FromMinutes(9), (CanalWise.S8, true))).EhNaoClassificada);
        Assert.Equal(motivo, MotorRegras.Classificar(regras, Contexto(TimeSpan.FromMinutes(10), (CanalWise.S8, true))).MotivoParadaId);
    }

    [Fact]
    public void RegraSemCondicoes_EhRejeitada()
    {
        Assert.Throws<ArgumentException>(() => MotorRegras.Preparar(
            [new Regra(Guid.NewGuid(), 1, [], TipoParada.Interna, Guid.NewGuid())]));
    }

    [Fact]
    public void CondicaoEmContador_EhRejeitada()
    {
        Assert.Throws<ArgumentException>(() => Condicao.EmAlarme(CanalWise.S2));
        Assert.Throws<ArgumentException>(() => Condicao.ForaDeAlarme(CanalWise.S3));
    }

    [Fact]
    public void TempoNegativo_EhRejeitado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Condicao.ParadaHaPeloMenos(TimeSpan.FromSeconds(-1)));
    }
}
