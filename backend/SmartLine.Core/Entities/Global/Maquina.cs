namespace SmartLine.Core.Entities.Global;

public class Maquina
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Fabricante { get; set; }
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public ICollection<MotivoParada> MotivosParada { get; set; } = [];
    public ICollection<TipoMedicao> TiposMedicao { get; set; } = [];
}
