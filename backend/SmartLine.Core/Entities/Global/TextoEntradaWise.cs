using SmartLine.Core.Iot;

namespace SmartLine.Core.Entities.Global;

/// <summary>
/// Texto personalizado de uma entrada do WISE para uma máquina do catálogo
/// (ex.: na Rotuladora, S8 = "Falta de garrafas na rotuladora"). Só existe
/// linha para o que foi personalizado; o resto usa <see cref="TextosEntradasWise.Padrao"/>.
/// </summary>
public class TextoEntradaWise
{
    public Guid Id { get; set; }
    public Guid MaquinaId { get; set; }
    public CanalWise Canal { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Só sensores de estado.</summary>
    public string? TextoAtivo { get; set; }

    /// <summary>Só sensores de estado.</summary>
    public string? TextoNormal { get; set; }

    // Navegação
    public Maquina Maquina { get; set; } = null!;
}
