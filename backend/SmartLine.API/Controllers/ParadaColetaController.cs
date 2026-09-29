using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

/// <summary>Paradas da coleta automática: pendentes de classificação e troca manual do motivo.</summary>
[ApiController]
[Route("api/coleta-iot/paradas")]
[Authorize]
public class ParadaColetaController : ControllerBase
{
    private readonly IClassificacaoParadaService _service;

    public ParadaColetaController(IClassificacaoParadaService service)
    {
        _service = service;
    }

    /// <summary>Paradas não classificadas, mais recentes primeiro. Todos os filtros são opcionais.</summary>
    [HttpGet("pendentes")]
    public async Task<IActionResult> Pendentes(
        [FromQuery] Guid? maquinaLinhaId,
        [FromQuery] Guid? linhaId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        [FromQuery] int limite = 200,
        CancellationToken cancellationToken = default) =>
        Ok(await _service.ListarPendentesAsync(
            new FiltroParadasPendentes(maquinaLinhaId, linhaId, Utc(desde), Utc(ate), limite), cancellationToken));

    /// <summary>Troca o motivo da parada. Fica no histórico com o usuário logado.</summary>
    [HttpPut("{paradaId:guid}/motivo")]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> Reclassificar(Guid paradaId, [FromBody] ReclassificarParadaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _service.ReclassificarAsync(paradaId, request.MotivoId, ObterUsuarioId(), cancellationToken);
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Valor);
    }

    /// <summary>Quem classificou a parada e quando, desde a classificação do sensor.</summary>
    [HttpGet("{paradaId:guid}/historico")]
    public async Task<IActionResult> Historico(Guid paradaId, CancellationToken cancellationToken)
    {
        var historico = await _service.HistoricoAsync(paradaId, cancellationToken);
        return historico is null ? NotFound() : Ok(historico);
    }

    /// <summary>Data vinda sem fuso (ex.: "2026-09-28T08:00") é tratada como UTC, como o resto da API.</summary>
    private static DateTime? Utc(DateTime? instante) => instante switch
    {
        null => null,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { Kind: DateTimeKind.Unspecified } semFuso => DateTime.SpecifyKind(semFuso, DateTimeKind.Utc),
        var utc => utc
    };

    private Guid ObterUsuarioId() => Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Usuário não autenticado"));
}

public record ReclassificarParadaRequest(Guid MotivoId);
