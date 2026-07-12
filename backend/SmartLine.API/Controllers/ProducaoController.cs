using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/producoes")]
[Authorize]
public class ProducaoController : ControllerBase
{
    private readonly IProducaoService _producaoService;

    public ProducaoController(IProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarProducaoRequest request)
    {
        var resultado = await _producaoService.RegistrarAsync(
            request.SessaoId,
            request.Quantidade,
            request.Refugo,
            request.Hora
        );
        return Ok(resultado);
    }
}

public record RegistrarProducaoRequest(
    Guid SessaoId,
    int Quantidade,
    int Refugo,
    DateTime Hora
);
