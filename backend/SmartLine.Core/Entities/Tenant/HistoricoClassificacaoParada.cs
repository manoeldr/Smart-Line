using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// Cada vez que o motivo de uma parada é definido ou trocado. O primeiro
/// registro de uma parada automática guarda a classificação original feita
/// pelo sistema, então reclassificações nunca apagam o que o sensor disse.
/// </summary>
public class HistoricoClassificacaoParada
{
    public Guid Id { get; set; }
    public Guid ParadaId { get; set; }

    /// <summary>Nulo quando a parada estava sem motivo (não classificada).</summary>
    public Guid? MotivoAnteriorId { get; set; }

    /// <summary>Nulo quando a parada voltou a ficar sem motivo.</summary>
    public Guid? MotivoNovoId { get; set; }

    /// <summary>Quem alterou. Nulo = o próprio sistema (classificação automática).</summary>
    public Guid? UsuarioId { get; set; }

    public DateTime AlteradoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Parada Parada { get; set; } = null!;
    public MotivoParada? MotivoAnterior { get; set; }
    public MotivoParada? MotivoNovo { get; set; }
    public Usuario? Usuario { get; set; }
}
