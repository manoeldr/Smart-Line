namespace SmartLine.Core.Iot;

/// <summary>
/// Canal marcado para leitura na medição.
/// </summary>
/// <param name="Multiplicador">
/// Garrafas por pulso, só para contadores. Ex.: sensor que conta caixas de 12
/// garrafas → 12. A produção é sempre medida em garrafas.
/// </param>
public sealed record CanalMedicao(CanalWise Canal, int Multiplicador = 1);

/// <summary>
/// Como uma máquina está sendo acompanhada: canais lidos, multiplicadores,
/// tempos e regras. Validada na construção; depois é imutável.
/// </summary>
public sealed class ConfiguracaoColetaIot
{
    /// <summary>Contadores lidos (produção e rejeito) e seus multiplicadores.</summary>
    public IReadOnlyDictionary<CanalWise, int> Multiplicadores { get; }

    /// <summary>Sensores de estado lidos (S1, S4, S7, S8 marcados).</summary>
    public IReadOnlySet<CanalWise> SensoresLidos { get; }

    /// <summary>
    /// Tempo sem incremento de produção para considerar a máquina parada (Z).
    /// Precisa ser maior que o filtro dos sensores de estado no WISE (10 s),
    /// senão a parada nasce não classificada antes de o sensor confirmar a
    /// causa e é reclassificada segundos depois.
    /// </summary>
    public TimeSpan TempoDeteccaoParada { get; }

    /// <summary>Tempo sem nenhuma mensagem para considerar a comunicação perdida.</summary>
    public TimeSpan TempoSemComunicacao { get; }

    /// <summary>Regras já ordenadas por prioridade.</summary>
    public IReadOnlyList<Regra> Regras { get; }

    /// <exception cref="ArgumentException">
    /// Nenhum contador de produção marcado, canal repetido, multiplicador
    /// inválido ou tempo não positivo.
    /// </exception>
    public ConfiguracaoColetaIot(
        IEnumerable<CanalMedicao> canais,
        TimeSpan tempoDeteccaoParada,
        TimeSpan tempoSemComunicacao,
        IEnumerable<Regra> regras)
    {
        var lista = canais.ToList();

        var repetido = lista.GroupBy(c => c.Canal).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new ArgumentException($"Canal {repetido.Key} marcado mais de uma vez.", nameof(canais));

        var multiplicadores = new Dictionary<CanalWise, int>();
        var sensores = new HashSet<CanalWise>();
        foreach (var c in lista)
        {
            var def = MapaWise.Definicao(c.Canal);
            if (def.Tipo == TipoCanal.Contador)
            {
                if (c.Multiplicador < 1)
                    throw new ArgumentException($"Multiplicador de {c.Canal} precisa ser 1 ou mais.", nameof(canais));
                multiplicadores[c.Canal] = c.Multiplicador;
            }
            else
            {
                sensores.Add(c.Canal);
            }
        }

        // A parada é detectada pelo contador de produção: sem ele não há como saber se a máquina roda.
        if (!multiplicadores.Keys.Any(c => MapaWise.Definicao(c).EhContadorProducao))
            throw new ArgumentException("Marque ao menos um contador de produção (S2, S5 ou S6).", nameof(canais));

        if (tempoDeteccaoParada <= TimeSpan.Zero)
            throw new ArgumentException("Tempo de detecção de parada precisa ser positivo.", nameof(tempoDeteccaoParada));
        if (tempoSemComunicacao <= TimeSpan.Zero)
            throw new ArgumentException("Tempo sem comunicação precisa ser positivo.", nameof(tempoSemComunicacao));

        Multiplicadores = multiplicadores;
        SensoresLidos = sensores;
        TempoDeteccaoParada = tempoDeteccaoParada;
        TempoSemComunicacao = tempoSemComunicacao;
        Regras = MotorRegras.Preparar(regras);
    }
}
