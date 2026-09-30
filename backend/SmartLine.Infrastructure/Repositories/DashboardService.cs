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
            var sessao = await _context.Sessoes
                .Include(s => s.Producoes)
                .Include(s => s.Paradas)
                    .ThenInclude(p => p.Motivo)
                .Where(s => s.MaquinaLinhaId == ml.Id
                    && s.Inicio >= inicio
                    && s.Inicio <= fim)
                .OrderByDescending(s => s.Status == StatusSessao.EmAndamento ? 1 : 0)
                .ThenByDescending(s => s.Inicio)
                .FirstOrDefaultAsync();

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

            var oee = _oeeService.Calcular(sessao, sessao.VelocidadeNominal, ml.MedeProducao);
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
}
