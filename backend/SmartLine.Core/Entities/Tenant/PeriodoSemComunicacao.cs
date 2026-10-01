namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// Intervalo em que a coleta automática não recebeu dados da máquina (WISE
/// fora do ar, Wi-Fi caído; no Automático, PLC inacessível). Não é parada da
/// máquina. Ligado à máquina da linha, e não ao dispositivo, para servir aos
/// dois modos.
/// </summary>
public class PeriodoSemComunicacao
{
    public Guid Id { get; set; }
    public Guid MaquinaLinhaId { get; set; }

    /// <summary>Instante da última mensagem antes do silêncio.</summary>
    public DateTime Inicio { get; set; }

    /// <summary>Instante em que a comunicação voltou; nulo enquanto continua fora.</summary>
    public DateTime? Fim { get; set; }

    /// <summary>
    /// Um contador voltou do zero durante o período (WISE reiniciado): a produção feita nele
    /// não pôde ser calculada.
    /// </summary>
    public bool ProducaoNaoRecuperada { get; set; }

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
}
