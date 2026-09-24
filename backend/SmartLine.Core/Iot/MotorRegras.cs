using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;

namespace SmartLine.Core.Iot;

/// <summary>Tipos de condição que uma regra pode testar.</summary>
public enum TipoCondicao
{
    /// <summary>O sensor de estado está em alarme (já considerando a polaridade).</summary>
    SensorEmAlarme,

    /// <summary>O sensor de estado está fora de alarme.</summary>
    SensorForaDeAlarme,

    /// <summary>A parada já dura pelo menos o tempo informado.</summary>
    TempoParadaMinimo
}

/// <summary>Uma condição de regra. Use as fábricas para montar.</summary>
public sealed record Condicao
{
    public TipoCondicao Tipo { get; }
    public CanalWise? Canal { get; }
    public TimeSpan? TempoMinimo { get; }

    private Condicao(TipoCondicao tipo, CanalWise? canal, TimeSpan? tempoMinimo)
    {
        Tipo = tipo;
        Canal = canal;
        TempoMinimo = tempoMinimo;
    }

    /// <exception cref="ArgumentException">Canal é contador.</exception>
    public static Condicao EmAlarme(CanalWise canal) => new(TipoCondicao.SensorEmAlarme, ValidarEstado(canal), null);

    /// <exception cref="ArgumentException">Canal é contador.</exception>
    public static Condicao ForaDeAlarme(CanalWise canal) => new(TipoCondicao.SensorForaDeAlarme, ValidarEstado(canal), null);

    /// <exception cref="ArgumentOutOfRangeException">Tempo negativo.</exception>
    public static Condicao ParadaHaPeloMenos(TimeSpan tempo) =>
        tempo >= TimeSpan.Zero
            ? new(TipoCondicao.TempoParadaMinimo, null, tempo)
            : throw new ArgumentOutOfRangeException(nameof(tempo), tempo, "Tempo mínimo não pode ser negativo.");

    /// <summary>
    /// Avalia no contexto. Sensor não lido na medição (ou que ainda não mandou
    /// valor) torna a condição falsa, nos dois sentidos: não dá para afirmar
    /// nem que está em alarme nem que está fora.
    /// </summary>
    public bool Avaliar(ContextoParada contexto) => Tipo switch
    {
        TipoCondicao.SensorEmAlarme =>
            contexto.Estados.TryGetValue(Canal!.Value, out var bruto)
            && MapaWise.Definicao(Canal.Value).EstaEmAlarme(bruto),
        TipoCondicao.SensorForaDeAlarme =>
            contexto.Estados.TryGetValue(Canal!.Value, out var bruto)
            && !MapaWise.Definicao(Canal.Value).EstaEmAlarme(bruto),
        TipoCondicao.TempoParadaMinimo => contexto.Duracao >= TempoMinimo!.Value,
        _ => throw new InvalidOperationException($"Condição {Tipo} sem avaliação.")
    };

    private static CanalWise ValidarEstado(CanalWise canal) =>
        MapaWise.Definicao(canal).Tipo == TipoCanal.Estado
            ? canal
            : throw new ArgumentException($"{canal} é contador; condições só usam sensores de estado.", nameof(canal));
}

/// <summary>
/// Regra de classificação: todas as condições verdadeiras (E) atribuem o motivo.
/// </summary>
/// <param name="Id">Identificação, gravada na parada para auditoria.</param>
/// <param name="Prioridade">Menor número = avaliada primeiro. Empate mantém a ordem da lista.</param>
/// <param name="Tipo">Tipo do motivo (copiado do MotivoParada ao carregar).</param>
public sealed record Regra(
    Guid Id,
    int Prioridade,
    IReadOnlyList<Condicao> Condicoes,
    TipoParada Tipo,
    Guid MotivoParadaId);

/// <summary>O que o motor sabe no momento de classificar.</summary>
/// <param name="Estados">Valor bruto dos sensores de estado lidos na medição.</param>
/// <param name="Duracao">Quanto a parada já dura.</param>
public sealed record ContextoParada(IReadOnlyDictionary<CanalWise, bool> Estados, TimeSpan Duracao);

/// <summary>
/// Classifica paradas a partir dos sensores. Declarativo de propósito: as
/// regras são dados editáveis por tela, sem script.
/// </summary>
public static class MotorRegras
{
    /// <summary>
    /// Ordena por prioridade e valida. Chamar uma vez ao carregar o conjunto,
    /// não a cada classificação.
    /// </summary>
    /// <exception cref="ArgumentException">Regra sem condições (bateria sempre).</exception>
    public static IReadOnlyList<Regra> Preparar(IEnumerable<Regra> regras)
    {
        var lista = regras.ToList();
        var semCondicao = lista.FirstOrDefault(r => r.Condicoes.Count == 0);
        if (semCondicao is not null)
        {
            throw new ArgumentException(
                $"Regra {semCondicao.Id} não tem condições e classificaria qualquer parada.", nameof(regras));
        }

        // OrderBy é estável: empate de prioridade mantém a ordem original.
        return lista.OrderBy(r => r.Prioridade).ToList();
    }

    /// <summary>
    /// Primeira regra (já ordenada por <see cref="Preparar"/>) com todas as
    /// condições verdadeiras. Nenhuma: <see cref="ClassificacaoParada.NaoClassificada"/>.
    /// </summary>
    public static ClassificacaoParada Classificar(IReadOnlyList<Regra> regrasPreparadas, ContextoParada contexto)
    {
        foreach (var regra in regrasPreparadas)
        {
            if (regra.Condicoes.All(c => c.Avaliar(contexto)))
            {
                return new ClassificacaoParada(regra.Tipo, regra.MotivoParadaId, regra.Id);
            }
        }

        return ClassificacaoParada.NaoClassificada;
    }
}
