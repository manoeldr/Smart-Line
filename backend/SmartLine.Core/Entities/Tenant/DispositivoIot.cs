namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// WISE-4051 instalado numa máquina de uma linha (um por máquina, por enquanto).
/// </summary>
public class DispositivoIot
{
    public Guid Id { get; set; }
    public Guid MaquinaLinhaId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Como o dispositivo se identifica nas mensagens MQTT (ClientId ou MAC,
    /// conforme o formato do firmware, a confirmar na bancada). É por aqui que
    /// uma mensagem chega à máquina certa.
    /// </summary>
    public string IdentificadorMqtt { get; set; } = string.Empty;

    /// <summary>Informativo, para diagnóstico de rede.</summary>
    public string? EnderecoIp { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Última mensagem recebida, para a tela de dispositivos.</summary>
    public DateTime? UltimaMensagemEm { get; set; }

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
}
