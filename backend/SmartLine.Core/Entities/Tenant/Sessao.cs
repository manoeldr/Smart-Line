using SmartLine.Core.Enums;

namespace SmartLine.Core.Entities.Tenant;

public class Sessao
{
    public Guid Id { get; set; }
    public Guid MaquinaLinhaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fim { get; set; }
    public StatusSessao Status { get; set; } = StatusSessao.EmAndamento;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<Producao> Producoes { get; set; } = [];
    public ICollection<Parada> Paradas { get; set; } = [];
    public ICollection<Medicao> Medicoes { get; set; } = [];
}
