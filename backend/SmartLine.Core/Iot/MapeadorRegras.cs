using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Iot;

/// <summary>
/// Converte as regras gravadas no banco para o formato do <see cref="MotorRegras"/>.
/// </summary>
public static class MapeadorRegras
{
    /// <summary>
    /// Regras ativas, já ordenadas por prioridade. Exige <c>MotivoParada</c> e
    /// <c>Condicoes</c> carregados (<c>Include</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Dado inconsistente no banco (condição de sensor sem canal, motivo não carregado).
    /// Melhor falhar ao iniciar a coleta do que classificar errado em silêncio.
    /// </exception>
    public static IReadOnlyList<Regra> ParaDominio(IEnumerable<RegraClassificacao> regras) =>
        MotorRegras.Preparar(regras.Where(r => r.Ativa).Select(Converter));

    private static Regra Converter(RegraClassificacao r)
    {
        if (r.MotivoParada is null)
            throw new InvalidOperationException($"Regra {r.Id}: motivo não carregado (faltou Include).");

        return new Regra(
            r.Id,
            r.Prioridade,
            r.Condicoes.Select(c => Converter(r, c)).ToList(),
            r.MotivoParada.Tipo,
            r.MotivoParadaId);
    }

    private static Condicao Converter(RegraClassificacao r, CondicaoRegra c) => c.Tipo switch
    {
        TipoCondicao.SensorEmAlarme => Condicao.EmAlarme(Canal(r, c)),
        TipoCondicao.SensorForaDeAlarme => Condicao.ForaDeAlarme(Canal(r, c)),
        TipoCondicao.TempoParadaMinimo => Condicao.ParadaHaPeloMenos(TimeSpan.FromSeconds(
            c.TempoMinimoSegundos ?? throw new InvalidOperationException(
                $"Regra {r.Id}: condição de tempo sem tempo mínimo."))),
        _ => throw new InvalidOperationException($"Regra {r.Id}: condição {c.Tipo} desconhecida.")
    };

    private static CanalWise Canal(RegraClassificacao r, CondicaoRegra c) =>
        c.Canal ?? throw new InvalidOperationException($"Regra {r.Id}: condição de sensor sem canal.");
}
