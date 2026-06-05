namespace SmartLine.Core.Entities.Global;

public class TipoMedicao
{
    public Guid Id { get; set; }
    public Guid MaquinaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    // Navegação
    public Maquina Maquina { get; set; } = null!;
}
