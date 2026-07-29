using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class LicencaService : ILicencaService
{
    private readonly SmartLineDbContext _context;

    // Segredo embutido no código — usado para assinar/validar chaves de licença.
    // Só quem tem esse valor consegue gerar uma chave válida para um MAC específico.
    // A ferramenta geradora de chaves (SmartLine.LicenseGenerator) usa o mesmo segredo.
    private const string Segredo = "SmartLine-Licenca-Sanmartin-2026-9f3a7c1e";

    public LicencaService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<LicencaStatusDto> ObterStatusAsync()
    {
        var mac = ObterMacAddress();
        var licenca = await _context.Licencas.FirstOrDefaultAsync();

        var ativa = licenca is not null
            && licenca.MacAddress == mac
            && licenca.Chave == GerarChaveEsperada(mac);

        return new LicencaStatusDto(ativa, mac);
    }

    public async Task<bool> AtivarAsync(string chave)
    {
        var mac = ObterMacAddress();
        var chaveNormalizada = NormalizarChave(chave);
        var chaveEsperada = GerarChaveEsperada(mac);

        if (chaveNormalizada != chaveEsperada) return false;

        var licencaExistente = await _context.Licencas.FirstOrDefaultAsync();
        if (licencaExistente is not null)
        {
            licencaExistente.Chave = chaveNormalizada;
            licencaExistente.MacAddress = mac;
            licencaExistente.AtivadaEm = DateTime.UtcNow;
        }
        else
        {
            _context.Licencas.Add(new Licenca
            {
                Id = Guid.NewGuid(),
                Chave = chaveNormalizada,
                MacAddress = mac,
                AtivadaEm = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // Detecta o MAC address da primeira interface de rede física ativa (Ethernet ou Wi-Fi).
    public string ObterMacAddress()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && ni.GetPhysicalAddress().GetAddressBytes().Length > 0)
            .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ToList();

        var escolhida = interfaces.FirstOrDefault();
        if (escolhida is null) return "SEM-MAC-DETECTADO";

        return escolhida.GetPhysicalAddress().ToString(); // formato: "0011223344AA"
    }

    // Gera a chave esperada para um MAC address, usando HMAC-SHA256 com o segredo fixo.
    // Formato final: 6 grupos de 4 caracteres hexadecimais separados por hífen.
    public string GerarChaveEsperada(string macAddress)
    {
        var macNormalizado = macAddress.ToUpperInvariant().Replace(":", "").Replace("-", "");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Segredo));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(macNormalizado));
        var hex = Convert.ToHexString(hash); // 64 caracteres, maiúsculo
        var truncado = hex.Substring(0, 24); // 24 caracteres = 6 grupos de 4

        var grupos = Enumerable.Range(0, 6).Select(i => truncado.Substring(i * 4, 4));
        return string.Join("-", grupos);
    }

	private static string NormalizarChave(string chave)
    	{
        	return chave.ToUpperInvariant().Replace(" ", "").Trim();
    	}
}