using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

/// <summary>
/// Regras de classificação automática das paradas (Semi Automático): padrão do
/// catálogo e personalização por máquina da linha. Mudanças valem para as
/// coletas iniciadas (ou retomadas) depois.
/// </summary>
[ApiController]
[Route("api/regras-classificacao")]
[Authorize]
public class RegrasClassificacaoController : ControllerBase
{
    private readonly IRegrasClassificacaoService _service;

    public RegrasClassificacaoController(IRegrasClassificacaoService service)
    {
        _service = service;
    }

    [HttpGet("catalogo/{maquinaId:guid}")]
    public async Task<IActionResult> DoCatalogo(Guid maquinaId, CancellationToken cancellationToken) =>
        Responder(await _service.DoCatalogoAsync(maquinaId, cancellationToken));

    /// <summary>Substitui as regras do catálogo. A ordem da lista é a prioridade.</summary>
    [HttpPut("catalogo/{maquinaId:guid}")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> SalvarDoCatalogo(Guid maquinaId, [FromBody] SalvarRegrasRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.SalvarDoCatalogoAsync(maquinaId, request, cancellationToken));

    [HttpPost("catalogo/{maquinaId:guid}/restaurar-padrao")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> RestaurarPadrao(Guid maquinaId, CancellationToken cancellationToken) =>
        Responder(await _service.RestaurarPadraoAsync(maquinaId, cancellationToken));

    /// <summary>Regras que valem para a máquina da linha (personalizadas ou, se não houver, as do catálogo).</summary>
    [HttpGet("maquina-linha/{maquinaLinhaId:guid}")]
    public async Task<IActionResult> DaMaquinaLinha(Guid maquinaLinhaId, CancellationToken cancellationToken) =>
        Responder(await _service.DaMaquinaLinhaAsync(maquinaLinhaId, cancellationToken));

    /// <summary>Personaliza as regras desta máquina da linha.</summary>
    [HttpPut("maquina-linha/{maquinaLinhaId:guid}")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> SalvarDaMaquinaLinha(Guid maquinaLinhaId, [FromBody] SalvarRegrasRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.SalvarDaMaquinaLinhaAsync(maquinaLinhaId, request, cancellationToken));

    /// <summary>Tira a personalização: a máquina volta às regras do catálogo.</summary>
    [HttpDelete("maquina-linha/{maquinaLinhaId:guid}")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> RemoverPersonalizacao(Guid maquinaLinhaId, CancellationToken cancellationToken) =>
        Responder(await _service.RemoverPersonalizacaoAsync(maquinaLinhaId, cancellationToken));

    private IActionResult Responder(ResultadoCadastro<ConjuntoRegrasDto> resultado)
    {
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Valor);
    }
}
