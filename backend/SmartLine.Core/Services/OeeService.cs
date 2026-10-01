using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;

namespace SmartLine.Core.Services;

public class OeeService : IOeeService
{
    public OeeResultado Calcular(Sessao sessao, decimal velocidadeNominal, bool medeProducao = true,
        IEnumerable<PeriodoSemComunicacao>? semComunicacao = null)
    {
        var fim = sessao.Fim ?? DateTime.UtcNow;
        // Sem comunicação fica fora do cálculo: não é tempo rodando nem parado (as paradas são
        // fechadas na última mensagem antes da queda, então não se sobrepõem a esses períodos).
        var tempoSemComunicacaoMs = TempoSemComunicacaoMs(semComunicacao, sessao.Inicio, fim);
        var tempoTotalMs = Math.Max(0, (fim - sessao.Inicio).TotalMilliseconds - tempoSemComunicacaoMs);

        // ── Separar paradas por tipo ────────────────────────────────
        // Parada sem motivo (não classificada) conta como Interna — ver Parada.TipoEfetivo.
        var paradasFinalizadas = sessao.Paradas
            .Where(p => p.Fim.HasValue)
            .ToList();

        var tempoPlanejadoMs = paradasFinalizadas
            .Where(p => p.TipoEfetivo() == TipoParada.Planejada)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        var tempoInternoMs = paradasFinalizadas
            .Where(p => p.TipoEfetivo() == TipoParada.Interna)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        var tempoExternoMs = paradasFinalizadas
            .Where(p => p.TipoEfetivo() == TipoParada.Externa)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        // Parada interna ainda em curso (sessão ativa)
        if (sessao.Status == StatusSessao.EmAndamento)
        {
            var paradaAtiva = sessao.Paradas.FirstOrDefault(p => !p.Fim.HasValue);
            if (paradaAtiva != null)
            {
                var duracaoAtiva = (DateTime.UtcNow - paradaAtiva.Inicio).TotalMilliseconds;
                var tipo = paradaAtiva.TipoEfetivo();
                switch (tipo)
                {
                    case TipoParada.Interna:
                        tempoInternoMs += duracaoAtiva;
                        break;
                    case TipoParada.Externa:
                        tempoExternoMs += duracaoAtiva;
                        break;
                    case TipoParada.Planejada:
                        tempoPlanejadoMs += duracaoAtiva;
                        break;
                }
            }
        }

        // ── Tempos derivados ──────────────────────────────────────
        var tempoDisponivelMs = Math.Max(0, tempoTotalMs - tempoPlanejadoMs);
        var tempoRodandoMs = Math.Max(0, tempoDisponivelMs - tempoInternoMs);

        // ── Disponibilidade ───────────────────────────────────────
        var disponibilidade = tempoDisponivelMs > 0
            ? tempoRodandoMs / tempoDisponivelMs * 100
            : 0.0;

        // ── Produção, refugo, Performance e OEE ──────────────────────
        // Máquinas sem contador de produção (MedeProducao = false) não têm como calcular
        // Performance nem OEE — ficam nulos (indisponíveis), não zero, pra distinguir de
        // "calculou e deu zero". Disponibilidade e Qualidade continuam calculáveis normalmente.
        int producao = 0;
        int refugo = 0;
        double? performance = null;
        double? oee = null;
        double qualidade = 100.0;

        if (medeProducao)
        {
            // Produção/Refugo é sempre a leitura do ÚLTIMO apontamento — mesmo padrão usado pelos
            // campos de coleta extras (temperatura, etc.), que também mostram o valor mais recente,
            // nunca uma soma de todos os apontamentos (cada leitura é o valor bruto do contador,
            // não um incremento desde a leitura anterior).
            var leituraInicial = sessao.Producoes.OrderBy(p => p.Hora).FirstOrDefault();
            var leituraFinal = sessao.Producoes.OrderByDescending(p => p.Hora).FirstOrDefault();

            producao = leituraFinal?.Quantidade ?? 0;
            refugo = leituraFinal?.Refugo ?? 0;

            // Produção/refugo REAL do turno (última leitura menos a inicial) — usado só internamente
            // para Performance e Qualidade, que precisam da quantidade produzida DURANTE a sessão,
            // não do valor absoluto do contador da máquina (que pode já vir de um total acumulado).
            // A produção feita sem comunicação (leituras marcadas) não tem tempo no cálculo: sai daqui.
            var (producaoSemComunicacao, refugoSemComunicacao) = ProducaoSemComunicacao(sessao);
            var producaoReal = leituraInicial is not null && leituraFinal is not null
                ? Math.Max(0, leituraFinal.Quantidade - leituraInicial.Quantidade - producaoSemComunicacao)
                : 0;
            var refugoReal = leituraInicial is not null && leituraFinal is not null
                ? Math.Max(0, leituraFinal.Refugo - leituraInicial.Refugo - refugoSemComunicacao)
                : 0;

            // Performance
            var tempoRodandoHoras = tempoRodandoMs / 3_600_000;
            var producaoEsperada = tempoRodandoHoras * (double)velocidadeNominal;
            performance = producaoEsperada > 0
                ? Math.Min(producaoReal / producaoEsperada * 100, 100)
                : 0.0;

            // Qualidade
            qualidade = producaoReal > 0 && refugoReal > 0
                ? Math.Max(0, (double)(producaoReal - refugoReal) / producaoReal * 100)
                : 100.0;

            // OEE
            oee = disponibilidade / 100 * (performance.Value / 100) * (qualidade / 100) * 100;
        }

        // ── Contagens ──────────────────────────────────────────────
        var numInternas = sessao.Paradas.Count(p => p.TipoEfetivo() == TipoParada.Interna);
        var numExternas = sessao.Paradas.Count(p => p.TipoEfetivo() == TipoParada.Externa);
        var numPlanejadas = sessao.Paradas.Count(p => p.TipoEfetivo() == TipoParada.Planejada);

        return new OeeResultado(
            TempoTotalMs: Math.Round(tempoTotalMs),
            TempoPlanejadoMs: Math.Round(tempoPlanejadoMs),
            TempoDisponivelMs: Math.Round(tempoDisponivelMs),
            TempoInternoMs: Math.Round(tempoInternoMs),
            TempoExternoMs: Math.Round(tempoExternoMs),
            TempoRodandoMs: Math.Round(tempoRodandoMs),
            Disponibilidade: Math.Round(disponibilidade, 1),
            Performance: performance.HasValue ? Math.Round(performance.Value, 1) : null,
            Qualidade: Math.Round(qualidade, 1),
            Oee: oee.HasValue ? Math.Round(oee.Value, 1) : null,
            Producao: producao,
            Refugo: refugo,
            NumParadas: sessao.Paradas.Count,
            NumParadasInternas: numInternas,
            NumParadasExternas: numExternas,
            NumParadasPlanejadas: numPlanejadas,
            TempoSemComunicacaoMs: Math.Round(tempoSemComunicacaoMs)
        );
    }

    /// <summary>Quanto dos períodos sem comunicação cai dentro da sessão (o aberto vai até o fim dela).</summary>
    public static double TempoSemComunicacaoMs(IEnumerable<PeriodoSemComunicacao>? periodos, DateTime inicio, DateTime fim) =>
        periodos?.Sum(p =>
        {
            var de = p.Inicio > inicio ? p.Inicio : inicio;
            var ate = p.Fim is { } f && f < fim ? f : fim;
            return ate > de ? (ate - de).TotalMilliseconds : 0;
        }) ?? 0;

    /// <summary>Produção e refugo que as leituras marcadas "sem comunicação" somaram.</summary>
    public static (int Producao, int Refugo) ProducaoSemComunicacao(Sessao sessao)
    {
        var leituras = sessao.Producoes.OrderBy(p => p.Hora).ToList();
        int producao = 0, refugo = 0;
        for (var i = 1; i < leituras.Count; i++)
        {
            if (!leituras[i].SemComunicacao) continue;
            producao += Math.Max(0, leituras[i].Quantidade - leituras[i - 1].Quantidade);
            refugo += Math.Max(0, leituras[i].Refugo - leituras[i - 1].Refugo);
        }
        return (producao, refugo);
    }
}