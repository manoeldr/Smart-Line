using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;

namespace SmartLine.Core.Services;

public class OeeService : IOeeService
{
    public OeeResultado Calcular(Sessao sessao, decimal velocidadeNominal)
    {
        var fim = sessao.Fim ?? DateTime.UtcNow;
        var tempoTotalMs = (fim - sessao.Inicio).TotalMilliseconds;

        // ── Separar paradas por tipo ──────────────────────────────
        var paradasFinalizadas = sessao.Paradas
            .Where(p => p.Fim.HasValue && p.Motivo is not null)
            .ToList();

        var tempoPlanejadoMs = paradasFinalizadas
            .Where(p => p.Motivo!.Tipo == TipoParada.Planejada)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        var tempoInternoMs = paradasFinalizadas
            .Where(p => p.Motivo!.Tipo == TipoParada.Interna)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        var tempoExternoMs = paradasFinalizadas
            .Where(p => p.Motivo!.Tipo == TipoParada.Externa)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        // Parada interna ainda em curso (sessão ativa)
        if (sessao.Status == StatusSessao.EmAndamento)
        {
            var paradaAtiva = sessao.Paradas.FirstOrDefault(p => !p.Fim.HasValue);
            if (paradaAtiva != null)
            {
                var duracaoAtiva = (DateTime.UtcNow - paradaAtiva.Inicio).TotalMilliseconds;
                var tipo = paradaAtiva.Motivo?.Tipo ?? TipoParada.Interna;
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

        // ── Tempos derivados ───────────────────────────────────────
        var tempoDisponivelMs = Math.Max(0, tempoTotalMs - tempoPlanejadoMs);
        var tempoRodandoMs = Math.Max(0, tempoDisponivelMs - tempoInternoMs);

        // ── Disponibilidade ────────────────────────────────────────
        var disponibilidade = tempoDisponivelMs > 0
            ? tempoRodandoMs / tempoDisponivelMs * 100
            : 0.0;

        // ── Produção e refugo ───────────────────────────────────────
        // Produção/Refugo é sempre a leitura do ÚLTIMO apontamento — mesmo padrão usado pelos
        // campos de coleta extras (temperatura, etc.), que também mostram o valor mais recente,
        // nunca uma soma de todos os apontamentos (cada leitura é o valor bruto do contador,
        // não um incremento desde a leitura anterior).
        var leituraInicial = sessao.Producoes.OrderBy(p => p.Hora).FirstOrDefault();
        var leituraFinal = sessao.Producoes.OrderByDescending(p => p.Hora).FirstOrDefault();

        var producao = leituraFinal?.Quantidade ?? 0;
        var refugo = leituraFinal?.Refugo ?? 0;

        // Produção/refugo REAL do turno (última leitura menos a inicial) — usado só internamente
        // para Performance e Qualidade, que precisam da quantidade produzida DURANTE a sessão,
        // não do valor absoluto do contador da máquina (que pode já vir de um total acumulado).
        var producaoReal = leituraInicial is not null && leituraFinal is not null
            ? Math.Max(0, leituraFinal.Quantidade - leituraInicial.Quantidade)
            : 0;
        var refugoReal = leituraInicial is not null && leituraFinal is not null
            ? Math.Max(0, leituraFinal.Refugo - leituraInicial.Refugo)
            : 0;

        // ── Performance ─────────────────────────────────────────────
        var tempoRodandoHoras = tempoRodandoMs / 3_600_000;
        var producaoEsperada = tempoRodandoHoras * (double)velocidadeNominal;
        var performance = producaoEsperada > 0
            ? Math.Min(producaoReal / producaoEsperada * 100, 100)
            : 0.0;

        // ── Qualidade ───────────────────────────────────────────────
        var qualidade = producaoReal > 0 && refugoReal > 0
            ? Math.Max(0, (double)(producaoReal - refugoReal) / producaoReal * 100)
            : 100.0;

        // ── OEE ─────────────────────────────────────────────────────
        var oee = disponibilidade / 100 * (performance / 100) * (qualidade / 100) * 100;

        // ── Contagens ───────────────────────────────────────────────
        var numInternas = sessao.Paradas.Count(p => p.Motivo?.Tipo == TipoParada.Interna);
        var numExternas = sessao.Paradas.Count(p => p.Motivo?.Tipo == TipoParada.Externa);
        var numPlanejadas = sessao.Paradas.Count(p => p.Motivo?.Tipo == TipoParada.Planejada);

        return new OeeResultado(
            TempoTotalMs: Math.Round(tempoTotalMs),
            TempoPlanejadoMs: Math.Round(tempoPlanejadoMs),
            TempoDisponivelMs: Math.Round(tempoDisponivelMs),
            TempoInternoMs: Math.Round(tempoInternoMs),
            TempoExternoMs: Math.Round(tempoExternoMs),
            TempoRodandoMs: Math.Round(tempoRodandoMs),
            Disponibilidade: Math.Round(disponibilidade, 1),
            Performance: Math.Round(performance, 1),
            Qualidade: Math.Round(qualidade, 1),
            Oee: Math.Round(oee, 1),
            Producao: producao,
            Refugo: refugo,
            NumParadas: sessao.Paradas.Count,
            NumParadasInternas: numInternas,
            NumParadasExternas: numExternas,
            NumParadasPlanejadas: numPlanejadas
        );
    }
}