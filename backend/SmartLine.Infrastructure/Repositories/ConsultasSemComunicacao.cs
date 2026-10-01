using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

/// <summary>Períodos sem comunicação de uma máquina que caem dentro de uma sessão.</summary>
public static class ConsultasSemComunicacao
{
    /// <param name="fim">Fim da sessão; nulo = em andamento (vai até agora).</param>
    public static Task<List<PeriodoSemComunicacao>> PeriodosSemComunicacaoAsync(
        this SmartLineDbContext context, Guid maquinaLinhaId, DateTime inicio, DateTime? fim) =>
        context.PeriodosSemComunicacao
            .AsNoTracking()
            .Where(p => p.MaquinaLinhaId == maquinaLinhaId
                        && (fim == null || p.Inicio < fim)
                        && (p.Fim == null || p.Fim > inicio))
            .OrderBy(p => p.Inicio)
            .ToListAsync();

    public static Task<List<PeriodoSemComunicacao>> PeriodosSemComunicacaoAsync(this SmartLineDbContext context, Sessao sessao) =>
        context.PeriodosSemComunicacaoAsync(sessao.MaquinaLinhaId, sessao.Inicio, sessao.Fim);
}
