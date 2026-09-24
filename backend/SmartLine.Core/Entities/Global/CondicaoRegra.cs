using SmartLine.Core.Iot;

namespace SmartLine.Core.Entities.Global;

/// <summary>
/// Condição de uma regra. Espelha <see cref="Condicao"/> do domínio:
/// <see cref="Canal"/> preenchido para condições de sensor,
/// <see cref="TempoMinimoSegundos"/> para condição de tempo.
/// </summary>
public class CondicaoRegra
{
    public Guid Id { get; set; }
    public Guid RegraClassificacaoId { get; set; }
    public TipoCondicao Tipo { get; set; }
    public CanalWise? Canal { get; set; }
    public int? TempoMinimoSegundos { get; set; }

    // Navegação
    public RegraClassificacao RegraClassificacao { get; set; } = null!;
}
