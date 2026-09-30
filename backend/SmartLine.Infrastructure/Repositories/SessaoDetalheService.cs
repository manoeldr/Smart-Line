using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class SessaoDetalheService : ISessaoDetalheService
{
    /// <summary>
    /// Soma a produção de cada hora cheia e marca o ponto no <b>início</b> da hora: o produzido
    /// das 14:00 às 14:59 aparece às 14:00, inclusive a hora ainda em andamento (com o parcial
    /// até agora). Uma gravação exatamente na hora cheia (ex.: 15:00, que traz o produzido das
    /// 14:55 às 15:00) é da hora que terminou.
    /// </summary>
    public static List<PontoProducaoDto> PorHora(IEnumerable<PontoProducaoDto> pontos) =>
        pontos
            .GroupBy(p => FimDaHora(p.Hora).AddHours(-1))
            .OrderBy(g => g.Key)
            .Select(g => new PontoProducaoDto(g.Key, g.Sum(p => p.Quantidade)))
            .ToList();

    /// <summary>
    /// Tempo parado por hora da sessão, por tipo: uma entrada por hora, do início da sessão até
    /// o fim (ou agora), inclusive as horas sem parada. Uma parada que atravessa horas entra
    /// em cada uma com o pedaço dela; a parada em curso conta até agora.
    /// </summary>
    public static List<ParadaPorHoraDto> ParadasPorHora(IEnumerable<Parada> paradas, DateTime inicioSessao, DateTime fimSessao)
    {
        var horas = new SortedDictionary<DateTime, double[]>(); // [interna, externa, planejada]
        for (var h = InicioDaHora(inicioSessao); h < fimSessao; h = h.AddHours(1))
            horas[h] = new double[3];

        foreach (var parada in paradas)
        {
            var inicio = parada.Inicio < inicioSessao ? inicioSessao : parada.Inicio;
            var fim = parada.Fim ?? fimSessao;
            if (fim > fimSessao) fim = fimSessao;
            var indice = parada.TipoEfetivo() switch
            {
                TipoParada.Externa => 1,
                TipoParada.Planejada => 2,
                _ => 0
            };

            for (var h = InicioDaHora(inicio); h < fim; h = h.AddHours(1))
            {
                var trecho = (Min(fim, h.AddHours(1)) - Max(inicio, h)).TotalMilliseconds;
                if (trecho <= 0) continue;
                if (!horas.TryGetValue(h, out var tempos))
                    horas[h] = tempos = new double[3];
                tempos[indice] += trecho;
            }
        }

        return horas
            .Select(h => new ParadaPorHoraDto(h.Key, Math.Round(h.Value[0]), Math.Round(h.Value[1]), Math.Round(h.Value[2])))
            .ToList();
    }

    /// <summary>
    /// Tempo parado e quantidade de paradas por motivo, do maior tempo para o menor. Sem motivo
    /// fica num grupo só ("Sem motivo", Interna). A parada em curso conta até <paramref name="agora"/>.
    /// </summary>
    public static List<ParadaPorMotivoDto> ParadasPorMotivo(IEnumerable<Parada> paradas, DateTime agora) =>
        paradas
            .GroupBy(p => p.MotivoId)
            .Select(g =>
            {
                var primeira = g.First();
                return new ParadaPorMotivoDto(
                    g.Key?.ToString(),
                    primeira.Motivo?.Nome ?? "Sem motivo",
                    primeira.TipoEfetivo().ToString(),
                    Math.Round(g.Sum(p => Math.Max(0, ((p.Fim ?? agora) - p.Inicio).TotalMilliseconds))),
                    g.Count());
            })
            .OrderByDescending(m => m.DuracaoMs)
            .ThenBy(m => m.Motivo)
            .ToList();

    private static DateTime InicioDaHora(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Kind);
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime FimDaHora(DateTime t)
    {
        var inicio = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Kind);
        return inicio == t ? t : inicio.AddHours(1);
    }

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
            .Where(p => p.Fim.HasValue && p.TipoEfetivo() != TipoParada.Planejada)
            .ToList();

        // MTTR — considera SÓ paradas Internas (não Externas). Faz sentido: MTTR mede o tempo
        // médio de "reparo", e uma parada Externa não é um reparo da própria máquina (ex: falta
        // de produto vindo de outra máquina da linha) — incluí-la no MTTR distorceria a métrica.
        var paradasInternas = paradasFalha
            .Where(p => p.TipoEfetivo() == TipoParada.Interna)
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

        // Semi Automático: a produção é gravada a cada 5 min, o que daria uma barra a cada 5 min.
        // O gráfico fica por hora, como no Manual.
        if (sessao.TipoColeta == TipoColeta.SemiAutomatico)
        {
            pontosProducao = PorHora(pontosProducao);
            if (sessao.Status == StatusSessao.EmAndamento)
            {
                var agora = DateTime.UtcNow;
                pontosProducao = pontosProducao
                    .Select(p => p.Hora.AddHours(1) > agora ? p with { Parcial = true } : p)
                    .ToList();
            }
        }

        // Gráficos de paradas: a parada em curso conta até agora (como no OEE).
        var agoraGraficos = DateTime.UtcNow;

        // Timeline de eventos (Marcha/Parada)
        var eventos = new List<EventoTimelineDto>();

        eventos.Add(new EventoTimelineDto("Marcha", sessao.Inicio, null, null, null, null));

        // Uma parada que continua noutra no mesmo instante (reclassificação da coleta automática,
        // virada do dia) não teve marcha no meio.
        var iniciosDeParada = sessao.Paradas.Select(p => p.Inicio).ToHashSet();

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
                parada.FotoPath,
                parada.Id.ToString(),
                parada.MotivoId?.ToString()
            ));

            if (parada.Fim.HasValue && !iniciosDeParada.Contains(parada.Fim.Value))
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
            Eventos: eventos,
            MaquinaId: maquinaLinha.MaquinaId.ToString(),
            TipoColeta: sessao.TipoColeta.ToString(),
            ParadasPorHora: ParadasPorHora(sessao.Paradas, sessao.Inicio, sessao.Fim ?? agoraGraficos),
            ParadasPorMotivo: ParadasPorMotivo(sessao.Paradas, agoraGraficos)
        );
    }
}