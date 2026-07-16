using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Entities.Tenant;

public class SessaoCampo
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public Guid CampoMaquinaId { get; set; }

    // Navegação
    public Sessao Sessao { get; set; } = null!;
    public CampoMaquina CampoMaquina { get; set; } = null!;
}
