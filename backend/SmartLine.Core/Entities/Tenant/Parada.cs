namespace SmartLine.Core.Entities.Tenant;

using SmartLine.Core.Entities.Global;

public class Parada
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public Guid MotivoId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fim { get; set; }

    // Navegação
    public Sessao Sessao { get; set; } = null!;
    public MotivoParada Motivo { get; set; } = null!;
}
