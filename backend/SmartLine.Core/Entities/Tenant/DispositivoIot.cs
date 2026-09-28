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
    /// IP fixo configurado no WISE (ex.: "192.168.10.21"). É por ele que uma
    /// mensagem chega à máquina certa: o broker anota o IP de cada conexão e a
    /// coleta procura o dispositivo com esse endereço. Por isso o WISE precisa
    /// de IP fixo (não DHCP). Gravado sempre na forma canônica (ver
    /// <c>EnderecoRede.Normalizar</c>), a mesma que o broker usa para o IP de
    /// quem conectou: só assim os dois batem.
    /// </summary>
    public string EnderecoIp { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Última mensagem recebida, para a tela de dispositivos.</summary>
    public DateTime? UltimaMensagemEm { get; set; }

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
}
