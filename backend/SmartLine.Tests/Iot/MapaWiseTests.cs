using SmartLine.Core.Iot;

namespace SmartLine.Tests.Iot;

public class MapaWiseTests
{
    [Fact]
    public void Mapa_TemOitoCanais_EmOrdem_ComEntradaI0aI7()
    {
        Assert.Equal(8, MapaWise.Canais.Count);
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal((CanalWise)(i + 1), MapaWise.Canais[i].Canal);
            Assert.Equal(i, MapaWise.Canais[i].Entrada);
        }
    }

    [Theory]
    [InlineData(CanalWise.S2)]
    [InlineData(CanalWise.S5)]
    [InlineData(CanalWise.S6)]
    public void ContadoresDeProducao(CanalWise canal)
    {
        var def = MapaWise.Definicao(canal);
        Assert.Equal(TipoCanal.Contador, def.Tipo);
        Assert.True(def.EhContadorProducao);
    }

    [Fact]
    public void S3_EhContadorDeRejeito_NaoDeProducao()
    {
        var def = MapaWise.Definicao(CanalWise.S3);
        Assert.Equal(TipoCanal.Contador, def.Tipo);
        Assert.False(def.EhContadorProducao);
    }

    // Sensores invertidos: 1 = sem presença, 0 = presença.
    [Theory]
    [InlineData(CanalWise.S1, true, true)]    // sem presença no acúmulo mínimo → alarme
    [InlineData(CanalWise.S1, false, false)]
    [InlineData(CanalWise.S8, true, true)]    // sem garrafas na entrada → alarme
    [InlineData(CanalWise.S8, false, false)]
    [InlineData(CanalWise.S4, false, true)]   // presença contínua na saída → alarme
    [InlineData(CanalWise.S4, true, false)]
    [InlineData(CanalWise.S7, false, true)]
    [InlineData(CanalWise.S7, true, false)]
    public void Polaridade_DosSensoresDeEstado(CanalWise canal, bool valorBruto, bool emAlarme)
    {
        Assert.Equal(emAlarme, MapaWise.Definicao(canal).EstaEmAlarme(valorBruto));
    }

    [Fact]
    public void Contador_NaoTemAlarme()
    {
        Assert.Throws<InvalidOperationException>(() => MapaWise.Definicao(CanalWise.S2).EstaEmAlarme(true));
    }

    [Fact]
    public void CanalInexistente_Falha()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapaWise.Definicao((CanalWise)9));
    }
}
