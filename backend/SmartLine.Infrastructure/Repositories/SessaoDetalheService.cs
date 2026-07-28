using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class SessaoDetalheService : ISessaoDetalheService
{
    private readonly SmartLineDbContext _context;
    private readonly IOeeService _oeeService;

    public SessaoDetalheService(SmartLineDbContext context, IOeeService oeeService)
    {
        _context = context;
        _oeeService = oeeService;
    }

    public async Task<SessaoDetalheDto?> GetUltimaSessaoDetalheAsync(Guid maquinaLinhaId)
    {
        var maquinaLinha = await _context.MaquinasLinha
            .Include(ml => ml.Maquina)
            .FirstOrDefaultAsync(ml => ml.Id == maquinaLinhaId);

        if (maquinaLinha is null) return null;

        var sessao = await _context.Sessoes
            .Include(s => s.Producoes)
            .Include(s => s.Paradas)
                .ThenInclude(p => p.Motivo)
            .Include(s => s.SessoesCampo)
                .ThenInclude(sc => sc.CampoMaquina)
            .Include(s => s.LeiturasExtra)
            .Where(s => s.MaquinaLinhaId == maquinaLinhaId)
            .OrderByDescending(s => s.Status == StatusSessao.EmAndamento ? 1 : 0)
            .ThenByDescending(s => s.Inicio)
            .FirstOrDefaultAsync();

        if (sessao is null) return null;

        var oeeResultado = _oeeService.Calcular(sessao, sessao.VelocidadeNominal);

        // MTTR / MTBF — considera apenas paradas não planejadas (Interna/Externa)
        var paradasFalha = sessao.Paradas
            .Where(p => p.Fim.HasValue && p.Motivo is not null && p.Motivo.Tipo != TipoParada.Planejada)
            .ToList();

        double? mttrMs = null;
        double? mtbfMs = null;

        if (paradasFalha.Count > 0)
        {
            var somaDuracaoParadas = paradasFalha.Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);
            mttrMs = somaDuracaoParadas / paradasFalha.Count;
            mtbfMs = oeeResultado.TempoRodandoMs / paradasFalha.Count;
        }

        // Campos extras para o gráfico
        var camposExtras = sessao.SessoesCampo
            .Select(sc => sc.CampoMaquina)
            .Distinct()
            .Select(campo => new CampoGraficoDto(
                campo.Id.ToString(),
                campo.Nome,
                campo.Unidade,
                sessao.LeiturasExtra
                    .Where(le => le.CampoMaquinaId == campo.Id)
                    .OrderBy(le => le.Hora)
                    .Select(le => new PontoExtraDto(le.Hora, le.Valor))
                    .ToList()
            ))
            .ToList();

        // Pontos de produção
        var pontosProducao = sessao.Producoes
            .OrderBy(p => p.Hora)
            .Select(p => new PontoProducaoDto(p.Hora, p.Quantidade))
            .ToList();

        // Timeline de eventos (Marcha/Parada)
        var eventos = new List<EventoTimelineDto>();

        eventos.Add(new EventoTimelineDto("Marcha", sessao.Inicio, null, null, null, null));

        foreach (var parada in sessao.Paradas.OrderBy(p => p.Inicio))
        {
            var duracao = parada.Fim.HasValue
                ? (parada.Fim.Value - parada.Inicio).TotalMilliseconds
                : (double?)null;

            eventos.Add(new EventoTimelineDto(
                "Parada",
                parada.Inicio,
                parada.Motivo?.Nome,
                parada.Motivo?.Tipo.ToString(),
                duracao,
                parada.FotoPath
            ));

            if (parada.Fim.HasValue)
            {
                eventos.Add(new EventoTimelineDto("Marcha", parada.Fim.Value, null, null, null, null));
            }
        }

        eventos = eventos.OrderBy(e => e.Horario).ToList();

        return new SessaoDetalheDto(
            SessaoId: sessao.Id.ToString(),
            MaquinaNome: maquinaLinha.Maquina.Nome,
            Inicio: sessao.Inicio,
            Fim: sessao.Fim,
            Status: sessao.Status.ToString(),
            VelocidadeNominal: sessao.VelocidadeNominal,
            SobreVelocidade: sessao.SobreVelocidade,
            Oee: oeeResultado.Oee,
            Eficiencia: oeeResultado.Performance,
            Disponibilidade: oeeResultado.Disponibilidade,
            Qualidade: oeeResultado.Qualidade,
            TempoRodandoMs: oeeResultado.TempoRodandoMs,
            TempoParadoMs: oeeResultado.TempoInternoMs + oeeResultado.TempoExternoMs,
            Producao: oeeResultado.Producao,
            Refugo: oeeResultado.Refugo,
            MttrMs: mttrMs,
            MtbfMs: mtbfMs,
            CamposExtras: camposExtras,
            PontosProducao: pontosProducao,
            Eventos: eventos
        );
    }
}