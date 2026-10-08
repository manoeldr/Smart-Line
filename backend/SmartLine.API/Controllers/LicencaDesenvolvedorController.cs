// Só existe no build de desenvolvimento (Debug): o publish de produção é Release e este arquivo
// fica fora do executável, então o pacote do cliente não gera chave de licença nem com o nível
// Desenvolvedor. Por cima disso, só responde com ASPNETCORE_ENVIRONMENT=Development.
#if DEBUG
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
    private readonly IHostEnvironment _ambiente;

    public LicencaDesenvolvedorController(ILicencaService licenca, IHostEnvironment ambiente)
    {
        _licenca = licenca;
        _ambiente = ambiente;
    }

    public sealed record SituacaoLicencaDevDto(bool LicencaAtiva, string MacPreferido, IReadOnlyList<string> MacsFisicos);
    public sealed record GerarChaveRequest(string MacAddress);
    public sealed record ChaveGeradaDto(string MacAddress, string Chave);

    /// <summary>404 fora do ambiente de desenvolvimento: a tela esconde a aba.</summary>
    [HttpGet]
    public async Task<ActionResult<SituacaoLicencaDevDto>> Situacao()
    {
        if (!_ambiente.IsDevelopment()) return NotFound();
        var status = await _licenca.ObterStatusAsync();
        return new SituacaoLicencaDevDto(status.Ativa, _licenca.ObterMacAddress(), _licenca.ObterMacsFisicos());
    }

    [HttpPost("gerar")]
    public ActionResult<ChaveGeradaDto> Gerar([FromBody] GerarChaveRequest pedido)
    {
        if (!_ambiente.IsDevelopment()) return NotFound();

        var mac = (pedido.MacAddress ?? "").Trim().ToUpperInvariant().Replace(":", "").Replace("-", "").Replace(" ", "");
        if (mac.Length != 12 || !mac.All(Uri.IsHexDigit))
            return BadRequest(new { mensagem = "Informe o MAC com 12 caracteres hexadecimais (ex.: 00-11-22-33-44-AA)." });

        return new ChaveGeradaDto(mac, _licenca.GerarChaveEsperada(mac));
    }
}
#endif
