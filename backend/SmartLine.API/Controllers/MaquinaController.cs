using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/maquinas")]
[Authorize]
public class MaquinaController : ControllerBase
{
    private readonly IMaquinaService _maquinaService;

    public MaquinaController(IMaquinaService maquinaService)
    {
        _maquinaService = maquinaService;
    }

    [HttpGet("{id}/motivos-parada")]
    public async Task<IActionResult> GetMotivosParada(Guid id)
    {
        var motivos = await _maquinaService.GetMotivosParadaAsync(id);
        return Ok(motivos);
    }
}
