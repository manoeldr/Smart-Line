using SmartLine.Core.Enums;
using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Entities.Tenant;

public class MaquinaLinha
{
    public Guid Id { get; set; }
    public Guid LinhaId { get; set; }
    public Guid MaquinaId { get; set; }
    public TipoColeta TipoColeta { get; set; }
    public decimal VelocidadeNominal { get; set; }
    public bool Critica { get; set; } = false;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    // Navegação
    public Linha Linha { get; set; } = null!;
    public Maquina Maquina { get; set; } = null!;
    public ICollection<Sessao> Sessoes { get; set; } = [];
}
