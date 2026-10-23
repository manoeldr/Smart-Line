// Vale também no pacote instalado (22/10): o suporte da Sanmartin, logado como Desenvolvedor,
// precisa ativar outro computador do cliente sem voltar para o escritório. A proteção é o nível
// Desenvolvedor, cuja senha deriva do segredo da licença e do MAC daquele computador — ninguém
// de fora entra. O segredo já viaja no executável (é com ele que a licença é conferida), então
// expor o gerador aqui não revela nada novo.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

/// <summary>Área do Desenvolvedor → Licença: gera a chave de um MAC, como o SmartLine.LicenseGenerator.</summary>
[ApiController]
[Route("api/dev/licenca")]
[Authorize(Policy = Politicas.Desenvolvedor)]
public class LicencaDesenvolvedorController : ControllerBase
{
    private readonly ILicencaService _licenca;

    public LicencaDesenvolvedorController(ILicencaService licenca)
    {
        _licenca = licenca;
    }

    public sealed record SituacaoLicencaDevDto(bool LicencaAtiva, string MacPreferido, IReadOnlyList<string> MacsFisicos, string SenhaDesenvolvedor);
    public sealed record GerarChaveRequest(string MacAddress);
    public sealed record ChaveGeradaDto(string MacAddress, string Chave, string SenhaDesenvolvedor);

    [HttpGet]
    public async Task<ActionResult<SituacaoLicencaDevDto>> Situacao()
    {
        var status = await _licenca.ObterStatusAsync();
        var mac = _licenca.ObterMacAddress();
        return new SituacaoLicencaDevDto(status.Ativa, mac, _licenca.ObterMacsFisicos(), _licenca.GerarSenhaDesenvolvedor(mac));
    }

    [HttpPost("gerar")]
    public ActionResult<ChaveGeradaDto> Gerar([FromBody] GerarChaveRequest pedido)
    {
        var mac = (pedido.MacAddress ?? "").Trim().ToUpperInvariant().Replace(":", "").Replace("-", "").Replace(" ", "");
        if (mac.Length != 12 || !mac.All(Uri.IsHexDigit))
            return BadRequest(new { mensagem = "Informe o MAC com 12 caracteres hexadecimais (ex.: 00-11-22-33-44-AA)." });

        return new ChaveGeradaDto(mac, _licenca.GerarChaveEsperada(mac), _licenca.GerarSenhaDesenvolvedor(mac));
    }
}
