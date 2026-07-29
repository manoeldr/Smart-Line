using System.Security.Cryptography;
using System.Text;

// Ferramenta de linha de comando para gerar chaves de licença do SmartLine.
// Uso: dotnet run -- <MAC_ADDRESS>
// O MAC address é o que aparece na tela de ativação do sistema no computador do cliente.

// IMPORTANTE: esse segredo precisa ser IDÊNTICO ao usado em LicencaService.cs no backend.
const string Segredo = "SmartLine-Licenca-Sanmartin-2026-9f3a7c1e";

if (args.Length == 0)
{
    Console.WriteLine("Uso: dotnet run -- <MAC_ADDRESS>");
    Console.WriteLine("Exemplo: dotnet run -- 0011223344AA");
    return;
}

var macAddress = args[0];
var chave = GerarChave(macAddress);

Console.WriteLine();
Console.WriteLine($"MAC address: {macAddress}");
Console.WriteLine($"Chave de licença: {chave}");
Console.WriteLine();

static string GerarChave(string macAddress)
{
    var macNormalizado = macAddress.ToUpperInvariant().Replace(":", "").Replace("-", "");

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Segredo));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(macNormalizado));
    var hex = Convert.ToHexString(hash);
    var truncado = hex.Substring(0, 24);

    var grupos = Enumerable.Range(0, 6).Select(i => truncado.Substring(i * 4, 4));
    return string.Join("-", grupos);
}