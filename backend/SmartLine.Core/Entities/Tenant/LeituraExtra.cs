using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Entities.Tenant;

public class LeituraExtra
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public Guid CampoMaquinaId { get; set; }
    public DateTime Hora { get; set; }
    public decimal Valor { get; set; }

    // Navegação
    public Sessao Sessao { get; set; } = null!;
    public CampoMaquina CampoMaquina { get; set; } = null!;
}
