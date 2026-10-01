using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class DashboardService : IDashboardService
{
    private readonly SmartLineDbContext _context;
    private readonly IOeeService _oeeService;

    public DashboardService(SmartLineDbContext context, IOeeService oeeService)
    {
        _context = context;
        _oeeService = oeeService;
    }

    /// <summary>
    /// Um card por máquina com os valores exatos de UMA sessão (não média): a em andamento,
    /// se houver, senão a última iniciada no período — a mesma que o detalhe da máquina
    /// mostra (ver <see cref="SessaoDetalheService"/>), com o mesmo cálculo de OEE.
    /// </summary>
    public async Task<IList<MaquinaDashboardDto>> GetDashboardLinhaAsync(Guid linhaId, DateTime inicio, DateTime fim)
    {
        var maquinasLinha = await _context.MaquinasLinha
            .Where(ml => ml.LinhaId == linhaId && ml.Ativo)
            .Include(ml => ml.Maquina)
            .OrderBy(ml => ml.Ordem)
            .ToListAsync();

        var resultado = new List<MaquinaDashboardDto>();

        foreach (var ml in maquinasLinha)
        {
            var sessao = await SessaoDaMaquinaAsync(ml.Id, inicio, fim);

            if (sessao is null)
            {
                resultado.Add(new MaquinaDashboardDto(
                    MaquinaLinhaId: ml.Id.ToString(),
                    MaquinaNome: ml.Maquina.Nome,
                    Critica: ml.Critica,
                    Oee: ml.MedeProducao ? 0 : null,
                    Disponibilidade: 0,
                    Performance: ml.MedeProducao ? 0 : null,
                    Qualidade: 0,
                    Producao: 0,
                    Refugo: 0,
                    TempoRodandoMs: 0,
                    TempoParadoMs: 0));
                continue;
            }

            var oee = _oeeService.Calcular(sessao, sessao.VelocidadeNominal, ml.MedeProducao,
                await _context.PeriodosSemComunicacaoAsync(sessao));
            var emAndamento = sessao.Status == StatusSessao.EmAndamento;

            resultado.Add(new MaquinaDashboardDto(
                MaquinaLinhaId: ml.Id.ToString(),
                MaquinaNome: ml.Maquina.Nome,
                Critica: ml.Critica,
                Oee: oee.Oee,
                Disponibilidade: oee.Disponibilidade,
                Performance: oee.Performance,
                Qualidade: oee.Qualidade,
                Producao: oee.Producao,
                Refugo: oee.Refugo,
                TempoRodandoMs: oee.TempoRodandoMs,
                TempoParadoMs: oee.TempoInternoMs + oee.TempoExternoMs,
                SessaoInicio: sessao.Inicio,
                SessaoFim: sessao.Fim,
                AoVivo: emAndamento,
                SituacaoAoVivo: !emAndamento ? null
                    : sessao.Paradas.Any(p => p.Fim is null) ? "Parada" : "Rodando",
                AcompanhamentoId: emAndamento ? sessao.AcompanhamentoId?.ToString() : null));
        }

        return resultado;
    }

    public async Task<LinhaDashboardDto> GetLinhaGeralAsync(Guid linhaId, DateTime inicio, DateTime fim)
    {
        var maquinasLinha = await _context.MaquinasLinha
            .Where(ml => ml.LinhaId == linhaId && ml.Ativo)
            .Include(ml => ml.Maquina)
            .OrderBy(ml => ml.Ordem)
            .ToListAsync();

        var agora = DateTime.UtcNow;
        var itens = new List<(Core.Entities.Tenant.MaquinaLinha Ml, Core.Entities.Tenant.Sessao? Sessao, OeeResultado? Oee)>();
        foreach (var ml in maquinasLinha)
        {
            var sessao = await SessaoDaMaquinaAsync(ml.Id, inicio, fim);
            itens.Add((ml, sessao, sessao is null ? null
                : _oeeService.Calcular(sessao, sessao.VelocidadeNominal, ml.MedeProducao, await _context.PeriodosSemComunicacaoAsync(sessao))));
        }

        // Referência do OEE da linha: a máquina crítica (a de pior OEE, se houver mais de uma);
        // sem crítica com sessão, a de pior OEE entre as que medem produção.
        var medem = itens.Where(i => i.Sessao is not null && i.Ml.MedeProducao).ToList();
        var criticas = medem.Where(i => i.Ml.Critica).ToList();
        var referencia = (criticas.Count > 0 ? criticas : medem)
            .OrderBy(i => i.Oee!.Oee ?? double.MaxValue)
            .Select(i => ((Core.Entities.Tenant.MaquinaLinha Ml, Core.Entities.Tenant.Sessao? Sessao, OeeResultado? Oee)?)i)
            .FirstOrDefault();

        // Paradas de cada máquina, com os mesmos cálculos do detalhe da máquina.
        var porMotivo = new Dictionary<(string Motivo, string Tipo), (double Ms, int Qtd, Dictionary<string, double> PorMaquina)>();
        var porHora = new SortedDictionary<DateTime, Dictionary<string, double>>();
        var resumos = new List<MaquinaResumoLinhaDto>();

        foreach (var (ml, sessao, oee) in itens)
        {
            var id = ml.Id.ToString();
            if (sessao is null)
            {
                resumos.Add(new MaquinaResumoLinhaDto(id, ml.Maquina.Nome, ml.Critica, false, false, false, null, 0, 0, 0, 0));
                continue;
            }

            var fimSessao = sessao.Fim ?? agora;
            var motivos = SessaoDetalheService.ParadasPorMotivo(sessao.Paradas, agora);
            foreach (var m in motivos)
            {
                var chave = (m.Motivo, m.Tipo);
                if (!porMotivo.TryGetValue(chave, out var atual))
                    atual = (0d, 0, new Dictionary<string, double>());
                atual.PorMaquina[id] = atual.PorMaquina.GetValueOrDefault(id) + m.DuracaoMs;
                porMotivo[chave] = (atual.Ms + m.DuracaoMs, atual.Qtd + m.Quantidade, atual.PorMaquina);
            }

            foreach (var h in SessaoDetalheService.ParadasPorHora(sessao.Paradas, sessao.Inicio, fimSessao))
            {
                if (!porHora.TryGetValue(h.Hora, out var daHora))
                    porHora[h.Hora] = daHora = new Dictionary<string, double>();
                daHora[id] = daHora.GetValueOrDefault(id) + h.InternaMs + h.ExternaMs + h.PlanejadaMs;
            }

            resumos.Add(new MaquinaResumoLinhaDto(
                id, ml.Maquina.Nome, ml.Critica,
                Referencia: referencia is not null && referencia.Value.Ml.Id == ml.Id,
                TemSessao: true,
                AoVivo: sessao.Status == StatusSessao.EmAndamento,
                Oee: oee!.Oee,
                Producao: oee.Producao,
                Refugo: oee.Refugo,
                TempoParadoMs: motivos.Sum(m => m.DuracaoMs),
                NumParadas: motivos.Sum(m => m.Quantidade)));
        }

        var paradasPorMotivo = porMotivo
            .Select(kv => new ParadaLinhaPorMotivoDto(
                kv.Key.Motivo, kv.Key.Tipo, kv.Value.Ms, kv.Value.Qtd,
                kv.Value.PorMaquina.Select(p => new TempoParadoMaquinaDto(p.Key, p.Value)).ToList()))
            .OrderByDescending(m => m.DuracaoMs)
            .ThenBy(m => m.Motivo)
            .ToList();

        var paradasPorHora = porHora
            .Select(kv => new ParadaLinhaPorHoraDto(kv.Key, kv.Value.Select(p => new TempoParadoMaquinaDto(p.Key, p.Value)).ToList()))
            .ToList();

        var r = referencia?.Oee;
        return new LinhaDashboardDto(
            MaquinaReferencia: referencia?.Ml.Maquina.Nome,
            ReferenciaCritica: referencia is not null && referencia.Value.Ml.Critica,
            Oee: r?.Oee,
            Disponibilidade: r?.Disponibilidade ?? 0,
            Performance: r?.Performance,
            Qualidade: r?.Qualidade ?? 0,
            Producao: r?.Producao ?? 0,
            RefugoTotal: resumos.Sum(m => m.Refugo),
            TempoParadoTotalMs: resumos.Sum(m => m.TempoParadoMs),
            NumParadas: resumos.Sum(m => m.NumParadas),
            Maquinas: resumos,
            ParadasPorMotivo: paradasPorMotivo,
            ParadasPorHora: paradasPorHora,
            // Produção por hora da máquina de referência (a mesma do OEE da linha).
            ProducaoPorHora: referencia?.Sessao is { } sessaoReferencia
                ? SessaoDetalheService.ProducaoPorHora(sessaoReferencia, agora, await _context.PeriodosSemComunicacaoAsync(sessaoReferencia))
                : []);
    }

    /// <summary>A sessão que o card e o detalhe da máquina mostram: a em andamento, senão a última do período.</summary>
    private async Task<Core.Entities.Tenant.Sessao?> SessaoDaMaquinaAsync(Guid maquinaLinhaId, DateTime inicio, DateTime fim) =>
        await _context.Sessoes
            .Include(s => s.Producoes)
            .Include(s => s.Paradas)
                .ThenInclude(p => p.Motivo)
            .Where(s => s.MaquinaLinhaId == maquinaLinhaId
                && s.Inicio >= inicio
                && s.Inicio <= fim)
            .OrderByDescending(s => s.Status == StatusSessao.EmAndamento ? 1 : 0)
            .ThenByDescending(s => s.Inicio)
            .FirstOrDefaultAsync();
}
