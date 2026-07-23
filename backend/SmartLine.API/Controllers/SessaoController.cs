using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;
using System.Security.Claims;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/sessoes")]
[Authorize]
public class SessaoController : ControllerBase
{
    private readonly ISessaoService _sessaoService;

    public SessaoController(ISessaoService sessaoService)
    {
        _sessaoService = sessaoService;
    }

    [HttpPost]
    public async Task<IActionResult> Abrir([FromBody] AbrirSessaoHttpRequest request)
    {
        var usuarioId = Guid.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new Exception("Usuário não autenticado")
        );

        var resultado = await _sessaoService.AbrirAsync(
            request.MaquinaLinhaId,
            usuarioId,
            new AbrirSessaoRequest(
                request.VelocidadeNominal,
                request.SobreVelocidade,
                request.PrevisaoTermino,
                request.TipoColeta,
                request.CampoMaquinaIds ?? new List<Guid>()
            )
        );

        if (resultado is null)
            return Conflict(new { mensagem = "Já existe uma sessão ativa para esta máquina." });
        return Ok(resultado);
    }

    [HttpPatch("{id}/fechar")]
    public async Task<IActionResult> Fechar(Guid id)
    {
        var sucesso = await _sessaoService.FecharAsync(id);
        if (!sucesso) return NotFound();
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var sessao = await _sessaoService.GetByIdAsync(id);
        if (sessao is null) return NotFound();
        return Ok(sessao);
    }

    [HttpPatch("{id}/estender")]
    public async Task<IActionResult> Estender(Guid id, [FromBody] EstenderSessaoRequest request)
    {
        var resultado = await _sessaoService.EstenderAsync(id, request.PrevisaoTermino);
        if (resultado is null) return NotFound();
        return Ok(resultado);
    }

    [HttpPatch("{id}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid id, [FromBody] FinalizarSessaoHttpRequest request)
    {
        var sucesso = await _sessaoService.FinalizarComLeituraAsync(
            id,
            new FinalizarSessaoRequest(
                request.ProducaoFinal,
                request.RefugoFinal,
                request.Extras ?? new List<LeituraExtraFinalRequest>()
            )
        );
        if (!sucesso) return NotFound();
        return NoContent();
    }
}

public record AbrirSessaoHttpRequest(
    Guid MaquinaLinhaId,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    DateTime? PrevisaoTermino,
    string TipoColeta,
    IList<Guid>? CampoMaquinaIds
);

public record EstenderSessaoRequest(DateTime PrevisaoTermino);

public record FinalizarSessaoHttpRequest(
    int ProducaoFinal,
    int RefugoFinal,
    IList<LeituraExtraFinalRequest>? Extras
);