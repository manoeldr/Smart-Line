namespace SmartLine.Core.Entities.Tenant;

public class Producao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public int Quantidade { get; set; }
    public int Refugo { get; set; } = 0;
    public DateTime Hora { get; set; } = DateTime.UtcNow;

    // Navegação
    public Sessao Sessao { get; set; } = null!;
}
