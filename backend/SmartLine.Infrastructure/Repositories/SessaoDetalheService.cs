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

        var oeeResultado = _oeeService.Calcular(sessao, sessao.VelocidadeNominal, maquinaLinha.MedeProducao);

        // MTBF — considera todas as paradas não planejadas (Interna/Externa), reflete o tempo
        // médio rodando entre uma parada e outra, qualquer que seja o motivo.
        var paradasFalha = sessao.Paradas
            .Where(p => p.Fim.HasValue && p.Motivo is not null && p.Motivo.Tipo != TipoParada.Planejada)
            .ToList();

        // MTTR — considera SÓ paradas Internas (não Externas). Faz sentido: MTTR mede o tempo
        // médio de "reparo", e uma parada Externa não é um reparo da própria máquina (ex: falta
        // de produto vindo de outra máquina da linha) — incluí-la no MTTR distorceria a métrica.
        var paradasInternas = paradasFalha
            .Where(p => p.Motivo!.Tipo == TipoParada.Interna)
            .ToList();

        double? mttrMs = null;
        double? mtbfMs = null;

        if (paradasInternas.Count > 0)
        {
            var somaDuracaoInternas = paradasInternas.Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);
            mttrMs = somaDuracaoInternas / paradasInternas.Count;
        }

        if (paradasFalha.Count > 0)
        {
            mtbfMs = oeeResultado.TempoRodandoMs / paradasFalha.Count;
        }

        // Campos extras para o gráfico — cada apontamento é sempre o valor ACUMULADO
        // (leitura bruta do contador), não um incremento desde a leitura anterior.
        // Pra virar "quanto foi produzido/registrado NAQUELA hora", calculamos a diferença
        // entre um apontamento e o anterior. O primeiro apontamento (leitura inicial) não
        // tem "hora anterior" pra comparar, então não entra no gráfico.
        var camposExtras = sessao.SessoesCampo
            .Select(sc => sc.CampoMaquina)
            .Distinct()
            .Select(campo =>
            {
                var leiturasOrdenadas = sessao.LeiturasExtra
                    .Where(le => le.CampoMaquinaId == campo.Id)
                    .OrderBy(le => le.Hora)
                    .ToList();

                var pontos = new List<PontoExtraDto>();
                for (var i = 1; i < leiturasOrdenadas.Count; i++)
                {
                    var diferenca = leiturasOrdenadas[i].Valor - leiturasOrdenadas[i - 1].Valor;
                    pontos.Add(new PontoExtraDto(leiturasOrdenadas[i].Hora, Math.Max(0, diferenca)));
                }

                return new CampoGraficoDto(campo.Id.ToString(), campo.Nome, campo.Unidade, pontos);
            })
            .ToList();

        // Pontos de produção — mesmo raciocínio: diferença entre apontamentos consecutivos,
        // primeiro apontamento (leitura inicial) não entra no gráfico.
        var producaoOrdenada = sessao.Producoes.OrderBy(p => p.Hora).ToList();
        var pontosProducao = new List<PontoProducaoDto>();
        for (var i = 1; i < producaoOrdenada.Count; i++)
        {
            var diferenca = producaoOrdenada[i].Quantidade - producaoOrdenada[i - 1].Quantidade;
            pontosProducao.Add(new PontoProducaoDto(producaoOrdenada[i].Hora, Math.Max(0, diferenca)));
        }

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