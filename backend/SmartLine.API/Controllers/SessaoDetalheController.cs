using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/maquinas-linha")]
[Authorize]
public class SessaoDetalheController : ControllerBase
{
    private readonly ISessaoDetalheService _service;

    public SessaoDetalheController(ISessaoDetalheService service)
    {
        _service = service;
    }

    [HttpGet("{maquinaLinhaId}/ultima-sessao-detalhe")]
    public async Task<IActionResult> GetUltimaSessaoDetalhe(Guid maquinaLinhaId)
    {
        var resultado = await _service.GetUltimaSessaoDetalheAsync(maquinaLinhaId);
        if (resultado is null) return NotFound();
        return Ok(resultado);
    }

    /// <summary>Detalhe de uma sessão específica (a do dia escolhido no filtro do Overview).</summary>
    [HttpGet("/api/sessoes/{sessaoId:guid}/detalhe")]
    public async Task<IActionResult> GetSessaoDetalhe(Guid sessaoId)
    {
        var resultado = await _service.GetSessaoDetalheAsync(sessaoId);
        if (resultado is null) return NotFound();
        return Ok(resultado);
    }
}