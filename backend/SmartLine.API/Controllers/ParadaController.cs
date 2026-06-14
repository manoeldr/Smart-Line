using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/paradas")]
[Authorize]
public class ParadaController : ControllerBase
{
    private readonly IParadaRegistroService _paradaService;

    public ParadaController(IParadaRegistroService paradaService)
    {
        _paradaService = paradaService;
    }

    [HttpPost]
    public async Task<IActionResult> Abrir([FromBody] AbrirParadaRequest request)
    {
        var parada = await _paradaService.AbrirAsync(request.SessaoId, request.Inicio);
        return Ok(parada);
    }

    [HttpPatch("{id}/fechar")]
    public async Task<IActionResult> Fechar(Guid id, [FromBody] FecharParadaRequest request)
    {
        var parada = await _paradaService.FecharAsync(id, request.MotivoId, request.Fim);
        if (parada is null) return NotFound();
        return Ok(parada);
    }
}

public record AbrirParadaRequest(Guid SessaoId, DateTime Inicio);
public record FecharParadaRequest(Guid MotivoId, DateTime Fim);
