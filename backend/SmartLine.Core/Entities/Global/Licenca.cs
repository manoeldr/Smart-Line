namespace SmartLine.Core.Entities.Global;

// Registro único por instalação — controla se essa máquina está licenciada para uso.
// A chave é validada por assinatura HMAC atrelada ao MAC address do computador.
public class Licenca
{
    public Guid Id { get; set; }
    public string Chave { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public DateTime AtivadaEm { get; set; }
}