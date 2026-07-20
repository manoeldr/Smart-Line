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
}

public record AbrirSessaoHttpRequest(
    Guid MaquinaLinhaId,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    DateTime? PrevisaoTermino,
    string TipoColeta,
    IList<Guid>? CampoMaquinaIds
);