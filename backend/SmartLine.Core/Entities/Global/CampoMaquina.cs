namespace SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;

public class CampoMaquina
{
    public Guid Id { get; set; }
    public Guid MaquinaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Unidade { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    // Navegação
    public Maquina Maquina { get; set; } = null!;
    public ICollection<SessaoCampo> SessoesCampo { get; set; } = [];
    public ICollection<LeituraExtra> LeiturasExtra { get; set; } = [];
}
