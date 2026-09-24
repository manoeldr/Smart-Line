using SmartLine.Core.Entities.Tenant;

namespace SmartLine.Core.Entities.Global;

/// <summary>
/// Regras de classificação automática de paradas (Semi Automático).
///
/// Existe em dois níveis, e exatamente um dos vínculos é preenchido:
/// <list type="bullet">
/// <item><b>Padrão do catálogo</b> (<see cref="MaquinaId"/>): vale para toda
/// máquina daquele tipo (ex.: toda Enchedora). Fica junto dos motivos de parada,
/// que também são do catálogo.</item>
/// <item><b>Customizado</b> (<see cref="MaquinaLinhaId"/>): cópia editável para
/// uma máquina específica de uma linha.</item>
/// </list>
/// Na hora de iniciar a coleta, vale o customizado da máquina da linha, se
/// existir; senão, o padrão do catálogo.
/// </summary>
public class ConjuntoRegras
{
    public Guid Id { get; set; }
    public Guid? MaquinaId { get; set; }
    public Guid? MaquinaLinhaId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Maquina? Maquina { get; set; }
    public MaquinaLinha? MaquinaLinha { get; set; }
    public ICollection<RegraClassificacao> Regras { get; set; } = [];
}
