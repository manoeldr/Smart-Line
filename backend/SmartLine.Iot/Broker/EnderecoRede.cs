using System.Net;

namespace SmartLine.Iot.Broker;

/// <summary>
/// Forma única de escrever um IP, usada tanto no broker (IP de quem conectou)
/// quanto no cadastro do WISE (IP digitado). Só assim os dois batem.
/// </summary>
public static class EnderecoRede
{
    /// <summary>
    /// IP canônico de uma conexão. O broker escuta IPv4 e IPv6 no mesmo socket,
    /// então um WISE em 192.168.10.21 pode aparecer como "::ffff:192.168.10.21";
    /// aqui isso volta a ser "192.168.10.21". Nulo se não for um endereço IP.
    /// </summary>
    public static string? Normalizar(EndPoint? endpoint) =>
        endpoint is IPEndPoint ip ? Normalizar(ip.Address) : null;

    /// <summary>
    /// IP canônico a partir do texto digitado no cadastro (aceita espaços nas
    /// pontas). Nulo se o texto não for um IP válido.
    /// </summary>
    public static string? Normalizar(string? texto) =>
        texto is not null && IPAddress.TryParse(texto.Trim(), out var ip) && TemFormaDeIp(texto.Trim())
            ? Normalizar(ip)
            : null;

    private static string Normalizar(IPAddress ip) =>
        (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();

    /// <summary>
    /// IPAddress.TryParse aceita formas estranhas ("10" vira 0.0.0.10, "1.2" vira
    /// 1.0.0.2). No cadastro, IPv4 só com os quatro números; IPv6 com ':'.
    /// </summary>
    private static bool TemFormaDeIp(string texto) =>
        texto.Contains(':') || texto.Split('.').Length == 4;
}
