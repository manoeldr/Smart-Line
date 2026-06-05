namespace SmartLine.Core.Entities.Tenant;

public class Linha
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Cliente Cliente { get; set; } = null!;
    public ICollection<MaquinaLinha> Maquinas { get; set; } = [];
}
