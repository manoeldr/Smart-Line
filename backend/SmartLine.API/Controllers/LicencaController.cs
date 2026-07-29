using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

// Sem [Authorize] — a licença precisa ser verificável antes mesmo do login.
[ApiController]
[Route("api/licenca")]
public class LicencaController : ControllerBase
{
    private readonly ILicencaService _licencaService;

    public LicencaController(ILicencaService licencaService)
    {
        _licencaService = licencaService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var status = await _licencaService.ObterStatusAsync();
        return Ok(status);
    }

    [HttpPost("ativar")]
    public async Task<IActionResult> Ativar([FromBody] AtivarLicencaRequest request)
    {
        var sucesso = await _licencaService.AtivarAsync(request.Chave);
        if (!sucesso) return BadRequest(new { mensagem = "Chave inválida para esta máquina." });
        return Ok();
    }
}

public record AtivarLicencaRequest(string Chave);