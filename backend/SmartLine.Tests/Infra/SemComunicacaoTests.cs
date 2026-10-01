using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Services;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>
/// WISE sem comunicação: o tempo fica fora do OEE; a produção que o contador mostra na volta
/// é gravada à parte (cinza no gráfico, dividida pelas horas do período) e não entra no OEE.
/// </summary>
public class SemComunicacaoTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    [Fact]
    public async Task NaVolta_ProducaoDoPeriodo_GravadaNumaLeituraMarcada()
    {
        await _c.Consolidar(Em(300), garrafas: 1000, rejeito: 2);
        await _c.Registrar(new ComunicacaoPerdida(Em(600)));

        await _c.Registrar(
            new ComunicacaoRestabelecida(Em(4200)),
            new ProducaoApurada(Em(4200), 3600, 3, SemComunicacao: true));

        var ultima = Assert.Single(_c.Sessoes()).Producoes.OrderBy(p => p.Hora).Last();
        Assert.Equal((4600, 5, Em(4200), true), (ultima.Quantidade, ultima.Refugo, ultima.Hora, ultima.SemComunicacao));
        var periodo = Assert.Single(_c.Periodos());
        Assert.Equal((Em(600), Em(4200), false), (periodo.Inicio, periodo.Fim!.Value, periodo.ProducaoNaoRecuperada));
    }

    [Fact]
    public async Task WiseReiniciadoNaQueda_PeriodoAvisaProducaoNaoRecuperada()
    {
        await _c.Registrar(new ComunicacaoPerdida(Em(600)));

        await _c.Registrar(new ComunicacaoRestabelecida(Em(4200), ProducaoNaoRecuperada: true));

        Assert.True(Assert.Single(_c.Periodos()).ProducaoNaoRecuperada);
    }

    [Fact]
    public async Task Detalhe_GraficoDivideAProducaoPelasHoras_ELinhaDoTempoTemOTrechoCinza()
    {
        // Coleta às 11:00; 100 até 11:05; sem comunicação das 11:10 às 12:10 (50 min na hora
        // das 11 e 10 min na das 12); na volta o contador tinha andado 3600.
        await _c.Consolidar(Em(300), garrafas: 100);
        await _c.Registrar(new ComunicacaoPerdida(Em(600)));
        await _c.Registrar(new ComunicacaoRestabelecida(Em(4200)), new ProducaoApurada(Em(4200), 3600, 0, SemComunicacao: true));

        await using var db = _c.Banco.NovoContexto();
        var detalhe = (await new SessaoDetalheService(db, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        Assert.Equal(
            new[] { (T0.UtcDateTime, 100, 3000), (T0.UtcDateTime.AddHours(1), 0, 600) },
            detalhe.PontosProducao.Select(p => (p.Hora, p.Quantidade, p.SemComunicacao)));

        Assert.Equal(
            new[] { ("Marcha", T0.UtcDateTime), ("SemComunicacao", Em(600)), ("Marcha", Em(4200)) },
            detalhe.Eventos.Select(e => (e.Tipo, e.Horario)));
        Assert.Equal(3_600_000d, detalhe.Eventos[1].DuracaoMs);
    }

    [Fact]
    public void DividirPorHora_ProporcionalAoTempo_ESomaFechaExato()
    {
        var onzeEMeia = new DateTime(2026, 9, 24, 11, 30, 0, DateTimeKind.Utc);

        var partes = SessaoDetalheService.DividirPorHora(3001, onzeEMeia, onzeEMeia.AddMinutes(90));

        Assert.Equal(
            new[] { (onzeEMeia.AddMinutes(-30), 1000), (onzeEMeia.AddMinutes(30), 2001) },
            partes);
        Assert.Equal(3001, partes.Sum(p => p.Quantidade));
    }

    [Fact]
    public void Oee_SemOTempoNemAProducaoDoPeriodoSemComunicacao()
    {
        // 2 h de sessão, 50 min sem comunicação; 3600 feitos com comunicação e 3000 sem.
        var inicio = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);
        var sessao = new Sessao
        {
            Id = Guid.NewGuid(), Inicio = inicio, Fim = inicio.AddHours(2), Status = StatusSessao.Finalizada,
            Paradas = [],
            Producoes =
            [
                new Producao { Quantidade = 0, Hora = inicio },
                new Producao { Quantidade = 3000, Hora = inicio.AddMinutes(50) },
                new Producao { Quantidade = 6000, Hora = inicio.AddMinutes(100), SemComunicacao = true },
                new Producao { Quantidade = 6600, Hora = inicio.AddHours(2) },
            ]
        };
        var periodo = new PeriodoSemComunicacao { Inicio = inicio.AddMinutes(50), Fim = inicio.AddMinutes(100) };

        var r = new OeeService().Calcular(sessao, 3600, true, [periodo]);

        // 70 min no cálculo, todos rodando: esperado 4200; produzido com comunicação 3600.
        Assert.Equal((4_200_000d, 3_000_000d, 100d), (r.TempoTotalMs, r.TempoSemComunicacaoMs, r.Disponibilidade));
        Assert.Equal(85.7, r.Performance);
        Assert.Equal(6600, r.Producao); // o contador continua mostrando tudo
    }
}
