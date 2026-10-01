namespace SmartLine.Core.Entities.Tenant;

public class Producao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public int Quantidade { get; set; }
    public int Refugo { get; set; } = 0;
    public DateTime Hora { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Leitura gravada na volta da comunicação (coleta automática): o que ela soma é a produção
    /// feita sem comunicação, que aparece à parte no gráfico e não entra no OEE.
    /// </summary>
    public bool SemComunicacao { get; set; }

    // Navegação
    public Sessao Sessao { get; set; } = null!;
}
