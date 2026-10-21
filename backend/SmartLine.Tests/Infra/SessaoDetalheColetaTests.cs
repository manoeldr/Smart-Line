using Microsoft.EntityFrameworkCore;
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
            new[] { ("Inicio", T0.UtcDateTime), ("Marcha", T0.UtcDateTime), ("Parada", Em(60)), ("Marcha", Em(300)) },
            detalhe.Eventos.Select(e => (e.Tipo, e.Horario)));
        Assert.Equal((parada.Id.ToString(), _c.Motivo("Falta de garrafas na entrada").ToString(), (double?)240_000),
            (detalhe.Eventos[2].ParadaId, detalhe.Eventos[2].MotivoId, detalhe.Eventos[2].DuracaoMs));
        Assert.Equal(_c.MaquinaCatalogo.ToString(), detalhe.MaquinaId);
    }

    [Fact]
    public async Task MtbfEMttr_SoComParadasInternas_EExternasTemIntervaloProprio()
    {
        // Internas (sem motivo): 60–300 s e 800–1000 s; externa: 400–700 s. Sessão fechada aos 1200 s.
        await _c.Registrar(
            new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada),
            new ParadaEncerrada(Em(300)),
            new ParadaIniciada(Em(400), _c.Classificacao("Falta de garrafas na entrada")),
            new ParadaEncerrada(Em(700)),
            new ParadaIniciada(Em(800), ClassificacaoParada.NaoClassificada),
            new ParadaEncerrada(Em(1000)));
        await using (var db = _c.Banco.NovoContexto())
        {
            var sessao = await db.Sessoes.SingleAsync(x => x.Id == _c.Sessao);
            sessao.Fim = Em(1200);
            sessao.Status = SmartLine.Core.Enums.StatusSessao.Finalizada;
            await db.SaveChangesAsync();
        }

        await using var leitura = _c.Banco.NovoContexto();
        var d = (await new SessaoDetalheService(leitura, new OeeService()).GetSessaoDetalheAsync(_c.Sessao))!;

        // Rodando = 1200 − internas 440 − externa 300 = 460 s; é o mesmo número em todo lugar.
        Assert.Equal(460_000d, d.TempoRodandoMs);
        Assert.Equal(740_000d, d.TempoParadoMs);
        Assert.Equal(d.TempoTotalMs, d.TempoRodandoMs + d.TempoInternoMs + d.TempoExternoMs + d.TempoPlanejadoMs + d.TempoSemComunicacaoMs);
        Assert.Equal((2, 1), (d.NumParadasInternas, d.NumParadasExternas));
        Assert.Equal(220_000d, d.MttrMs);                    // 440 ÷ 2
        Assert.Equal(230_000d, d.MtbfMs);                    // rodando 460 ÷ 2 internas (a externa não é falha)
        Assert.Equal(460_000d, d.IntervaloMedioExternasMs);  // rodando ÷ 1 externa
    }

    [Fact]
    public async Task DetalhePorSessao_EOMesmoDaUltima_QuandoEAMesmaSessao()
    {
        await _c.Consolidar(Em(300), garrafas: 100);

        await using var db = _c.Banco.NovoContexto();
        var servico = new SessaoDetalheService(db, new OeeService());
        var porSessao = await servico.GetSessaoDetalheAsync(_c.Sessao);
        var ultima = await servico.GetUltimaSessaoDetalheAsync(_c.MaquinaLinha);

        Assert.Equal(ultima!.SessaoId, porSessao!.SessaoId);
        Assert.Equal((ultima.Producao, ultima.Oee, ultima.Eventos.Count), (porSessao.Producao, porSessao.Oee, porSessao.Eventos.Count));
        Assert.Null(await servico.GetSessaoDetalheAsync(Guid.NewGuid()));
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

        // 11:00–11:59 às 11:00 (a gravação das 12:00 é dessa hora); a hora em andamento às 12:00.
        Assert.Equal(new[] { (Em(0), 1000), (Em(3600), 100) }, detalhe.PontosProducao.Select(p => (p.Hora, p.Quantidade)));
    }

    [Fact]
    public void PorHora_NoInicioDaHora_GravacaoNaHoraCheiaEDaHoraQueTerminou()
    {
        var t = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

        var pontos = SessaoDetalheService.PorHora(
        [
            new(t.AddMinutes(5), 10), new(t.AddMinutes(55), 20), new(t.AddHours(1), 30), // 10:00–11:00
            new(t.AddHours(1).AddMinutes(5), 40), new(t.AddHours(2), 50),                 // 11:00–12:00
            new(t.AddHours(2).AddMinutes(58), 5),                                          // 12:00–13:00, só até 12:58
            new(t.AddHours(3).AddMinutes(10), 7),                                          // 13:00–14:00 em andamento
        ]);

        Assert.Equal(
            new[] { (t, 60), (t.AddHours(1), 90), (t.AddHours(2), 5), (t.AddHours(3), 7) },
            pontos.Select(p => (p.Hora, p.Quantidade)));
    }
}
