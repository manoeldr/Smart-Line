namespace SmartLine.Core.Entities.Tenant;

public class Cliente
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Estado { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<Linha> Linhas { get; set; } = [];
}
