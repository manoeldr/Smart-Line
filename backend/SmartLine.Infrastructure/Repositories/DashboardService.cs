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
            var sessoes = await _context.Sessoes
                .Include(s => s.Producoes)
                .Include(s => s.Paradas)
                    .ThenInclude(p => p.Motivo)
                .Where(s => s.MaquinaLinhaId == ml.Id
                    && s.Status == StatusSessao.Finalizada
                    && s.Inicio >= inicio
                    && s.Inicio <= fim)
                .ToListAsync();

            if (sessoes.Count == 0)
            {
                resultado.Add(new MaquinaDashboardDto(
                    MaquinaLinhaId: ml.Id.ToString(),
                    MaquinaNome: ml.Maquina.Nome,
                    Critica: ml.Critica,
                    Oee: 0,
                    Disponibilidade: 0,
                    Performance: 0,
                    Qualidade: 0,
                    Producao: 0,
                    Refugo: 0,
                    NumSessoes: 0,
                    TempoRodandoMs: 0,
                    TempoParadoMs: 0
                ));
                continue;
            }

            double somaTempoDisponivel = 0;
            double somaTempoRodando = 0;
            double somaTempoParado = 0;
            double somaDisponibilidadePonderada = 0;
            double somaPerformancePonderada = 0;
            double somaQualidadePonderada = 0;
            int producaoTotal = 0;
            int refugoTotal = 0;

            foreach (var sessao in sessoes)
            {
                var oee = _oeeService.Calcular(sessao, sessao.VelocidadeNominal);

                somaTempoDisponivel += oee.TempoDisponivelMs;
                somaTempoRodando += oee.TempoRodandoMs;
                somaTempoParado += (oee.TempoInternoMs + oee.TempoExternoMs);
                producaoTotal += oee.Producao;
                refugoTotal += oee.Refugo;

                // Pondera pelo tempo disponível de cada sessão
                somaDisponibilidadePonderada += oee.Disponibilidade * oee.TempoDisponivelMs;
                somaPerformancePonderada += oee.Performance * oee.TempoDisponivelMs;
                somaQualidadePonderada += oee.Qualidade * oee.TempoDisponivelMs;
            }

            var disponibilidadeMedia = somaTempoDisponivel > 0 ? somaDisponibilidadePonderada / somaTempoDisponivel : 0;
            var performanceMedia = somaTempoDisponivel > 0 ? somaPerformancePonderada / somaTempoDisponivel : 0;
            var qualidadeMedia = somaTempoDisponivel > 0 ? somaQualidadePonderada / somaTempoDisponivel : 0;
            var oeeMedio = (disponibilidadeMedia / 100) * (performanceMedia / 100) * (qualidadeMedia / 100) * 100;

            resultado.Add(new MaquinaDashboardDto(
                MaquinaLinhaId: ml.Id.ToString(),
                MaquinaNome: ml.Maquina.Nome,
                Critica: ml.Critica,
                Oee: Math.Round(oeeMedio, 1),
                Disponibilidade: Math.Round(disponibilidadeMedia, 1),
                Performance: Math.Round(performanceMedia, 1),
                Qualidade: Math.Round(qualidadeMedia, 1),
                Producao: producaoTotal,
                Refugo: refugoTotal,
                NumSessoes: sessoes.Count,
                TempoRodandoMs: somaTempoRodando,
                TempoParadoMs: somaTempoParado
            ));
        }

        return resultado;
    }
}