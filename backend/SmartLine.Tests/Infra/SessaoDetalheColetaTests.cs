using SmartLine.Core.Coleta;
using SmartLine.Core.Services;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>Linha do tempo do detalhe da máquina com paradas da coleta automática.</summary>
public class SessaoDetalheColetaTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    [Fact]
    public async Task Paradas_TrazemIdEMotivo_EReclassificacaoNaoInventaMarchaNoMeio()
    {
        // Sem causa às 60 s; aos 200 s o sensor passa a explicar (fecha um trecho e abre outro).
        await _c.Registrar(
            new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada),
            new ParadaReclassificada(Em(200), _c.Classificacao("Falta de garrafas na entrada")),
            new ParadaEncerrada(Em(300)));

        await using var db = _c.Banco.NovoContexto();
        var detalhe = (await new SessaoDetalheService(db, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        var paradas = _c.Paradas();
        Assert.Equal(
            new[] { ("Marcha", T0.UtcDateTime), ("Parada", Em(60)), ("Parada", Em(200)), ("Marcha", Em(300)) },
            detalhe.Eventos.Select(e => (e.Tipo, e.Horario)));
        Assert.Equal((paradas[0].Id.ToString(), (string?)null), (detalhe.Eventos[1].ParadaId, detalhe.Eventos[1].MotivoId));
        Assert.Equal((paradas[1].Id.ToString(), _c.Motivo("Falta de garrafas na entrada").ToString()), (detalhe.Eventos[2].ParadaId, detalhe.Eventos[2].MotivoId));
        Assert.Equal(_c.MaquinaCatalogo.ToString(), detalhe.MaquinaId);
    }
}
