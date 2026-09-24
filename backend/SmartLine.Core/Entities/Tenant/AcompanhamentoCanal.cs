using SmartLine.Core.Iot;

namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// Canal do WISE marcado para leitura num acompanhamento. Canal não marcado
/// não tem registro.
/// </summary>
public class AcompanhamentoCanal
{
    public Guid Id { get; set; }
    public Guid AcompanhamentoId { get; set; }
    public CanalWise Canal { get; set; }

    /// <summary>Garrafas por pulso (só contadores; 1 nos sensores de estado).</summary>
    public int Multiplicador { get; set; } = 1;

    /// <summary>
    /// Último valor bruto do contador, gravado a cada consolidação. Permite
    /// retomar a coleta depois de o backend reiniciar sem perder a produção
    /// feita enquanto ele esteve fora. É <c>long</c> porque o contador do WISE
    /// é <c>uint</c> e o SQLite não tem inteiro sem sinal.
    /// </summary>
    public long? UltimoValorBruto { get; set; }

    public DateTime? UltimoValorEm { get; set; }

    // Navegação
    public Acompanhamento Acompanhamento { get; set; } = null!;
}
