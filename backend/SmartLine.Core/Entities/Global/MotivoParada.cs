using SmartLine.Core.Enums;

namespace SmartLine.Core.Entities.Global;

public class MotivoParada
{
    public Guid Id { get; set; }
    public Guid MaquinaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoParada Tipo { get; set; }
    public bool Ativo { get; set; } = true;

    // Navegação
    public Maquina Maquina { get; set; } = null!;
}
