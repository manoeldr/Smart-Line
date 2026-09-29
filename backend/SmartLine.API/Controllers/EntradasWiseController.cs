using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

/// <summary>Textos das entradas do WISE de uma máquina do catálogo.</summary>
[ApiController]
[Route("api/maquinas/{maquinaId:guid}/entradas-wise")]
[Authorize]
public class EntradasWiseController : ControllerBase
{
    private readonly IEntradasWiseService _service;

    public EntradasWiseController(IEntradasWiseService service)
    {
        _service = service;
    }

    /// <summary>As 8 entradas com o texto em uso (personalizado ou padrão).</summary>
    [HttpGet]
    public async Task<IActionResult> Obter(Guid maquinaId, CancellationToken cancellationToken)
    {
        var entradas = await _service.ObterAsync(maquinaId, cancellationToken);
        return entradas is null ? NotFound() : Ok(entradas);
    }

    /// <summary>Personaliza as entradas enviadas; as outras ficam como estão.</summary>
    [HttpPut]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> Salvar(Guid maquinaId, [FromBody] IList<SalvarEntradaWiseRequest> entradas, CancellationToken cancellationToken)
    {
        var resultado = await _service.SalvarAsync(maquinaId, entradas, cancellationToken);
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Valor);
    }

    [HttpPost("restaurar-padrao")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> RestaurarPadrao(Guid maquinaId, CancellationToken cancellationToken)
    {
        var entradas = await _service.RestaurarPadraoAsync(maquinaId, cancellationToken);
        return entradas is null ? NotFound() : Ok(entradas);
    }
}
