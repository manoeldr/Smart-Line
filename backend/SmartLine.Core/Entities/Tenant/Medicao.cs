namespace SmartLine.Core.Entities.Tenant;

using SmartLine.Core.Entities.Global;

public class Medicao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public Guid TipoMedicaoId { get; set; }
    public decimal Valor { get; set; }
    public DateTime Hora { get; set; } = DateTime.UtcNow;

    // Navegação
    public Sessao Sessao { get; set; } = null!;
    public TipoMedicao TipoMedicao { get; set; } = null!;
}
