using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/leituras-extra")]
[Authorize]
public class LeituraExtraController : ControllerBase
{
    private readonly ILeituraExtraService _leituraExtraService;

    public LeituraExtraController(ILeituraExtraService leituraExtraService)
    {
        _leituraExtraService = leituraExtraService;
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarLeituraExtraRequest request)
    {
        var resultado = await _leituraExtraService.RegistrarAsync(
            request.SessaoId,
            request.CampoMaquinaId,
            request.Valor,
            request.Hora
        );
        return Ok(resultado);
    }
}

public record RegistrarLeituraExtraRequest(
    Guid SessaoId,
    Guid CampoMaquinaId,
    decimal Valor,
    DateTime Hora
);
