using SmartLine.Core.Enums;

namespace SmartLine.Core.Entities.Tenant;

public class Usuario
{
    public Guid Id { get; set; }
    public Guid? ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public NivelUsuario Nivel { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Cliente? Cliente { get; set; }
}
