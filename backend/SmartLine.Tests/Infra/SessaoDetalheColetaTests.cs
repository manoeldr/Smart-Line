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
    public async Task Paradas_TrazemIdEMotivo_ECausaQueApareceNoMeioNaoViraOutraParada()
    {
        // Sem causa às 60 s; aos 200 s o sensor passa a explicar: a mesma parada ganha o motivo.
        await _c.Registrar(
            new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada),
            new ParadaReclassificada(Em(200), _c.Classificacao("Falta de garrafas na entrada")),
            new ParadaEncerrada(Em(300)));

        await using var db = _c.Banco.NovoContexto();
        var detalhe = (await new SessaoDetalheService(db, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        var parada = Assert.Single(_c.Paradas());
        Assert.Equal(
            new[] { ("Marcha", T0.UtcDateTime), ("Parada", Em(60)), ("Marcha", Em(300)) },
            detalhe.Eventos.Select(e => (e.Tipo, e.Horario)));
        Assert.Equal((parada.Id.ToString(), _c.Motivo("Falta de garrafas na entrada").ToString(), (double?)240_000),
            (detalhe.Eventos[1].ParadaId, detalhe.Eventos[1].MotivoId, detalhe.Eventos[1].DuracaoMs));
        Assert.Equal(_c.MaquinaCatalogo.ToString(), detalhe.MaquinaId);
    }

    [Fact]
    public async Task GraficoDeProducao_PorHora_EmVezDeACada5Minutos()
    {
        // Coleta às 11:00; gravações às 11:05, 11:10, 12:00 e 12:05.
        await _c.Consolidar(Em(300), garrafas: 100);
        await _c.Consolidar(Em(600), garrafas: 150);
        await _c.Consolidar(Em(3600), garrafas: 750);
        await _c.Consolidar(Em(3900), garrafas: 100);

        await using var db = _c.Banco.NovoContexto();
        var detalhe = (await new SessaoDetalheService(db, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        // 11:00–12:00 inteira às 12:00; a hora em andamento na última gravação (12:05).
        Assert.Equal(new[] { (Em(3600), 1000), (Em(3900), 100) }, detalhe.PontosProducao.Select(p => (p.Hora, p.Quantidade)));
    }

    [Fact]
    public void PorHora_HoraCheiaNoFim_EGravacaoExatamenteNaHoraFicaNaHoraQueTerminou()
    {
        var t = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

        var pontos = SessaoDetalheService.PorHora(
        [
            new(t.AddMinutes(5), 10), new(t.AddMinutes(55), 20), new(t.AddHours(1), 30), // 10:00–11:00
            new(t.AddHours(1).AddMinutes(5), 40), new(t.AddHours(2), 50),                 // 11:00–12:00
        ]);

        Assert.Equal(new[] { (t.AddHours(1), 60), (t.AddHours(2), 90) }, pontos.Select(p => (p.Hora, p.Quantidade)));
    }
}
