using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/configuracao/linhas/{linhaId}/maquinas")]
[Authorize]
public class LinhaMaquinaController : ControllerBase
{
    private readonly ILinhaMaquinaService _service;

    public LinhaMaquinaController(ILinhaMaquinaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetMaquinas(Guid linhaId)
    {
        var maquinas = await _service.GetMaquinasDaLinhaAsync(linhaId);
        return Ok(maquinas);
    }

    [HttpPost]
    public async Task<IActionResult> Adicionar(Guid linhaId, [FromBody] AdicionarMaquinaLinhaRequest request)
    {
        if (ErroTempoDeteccao(request.TempoDeteccaoParadaSegundos) is { } erro)
            return BadRequest(new { mensagem = erro });

        var resultado = await _service.AdicionarMaquinaAsync(
            linhaId,
            request.MaquinaId,
            request.Critica,
            request.VelocidadeNominal,
            request.SobreVelocidade,
            request.MedeProducao,
            request.TempoDeteccaoParadaSegundos
        );
        return Ok(resultado);
    }

    [HttpPut("{maquinaLinhaId}")]
    public async Task<IActionResult> Atualizar(Guid linhaId, Guid maquinaLinhaId, [FromBody] AtualizarMaquinaLinhaRequest request)
    {
        if (ErroTempoDeteccao(request.TempoDeteccaoParadaSegundos) is { } erro)
            return BadRequest(new { mensagem = erro });

        var resultado = await _service.AtualizarAsync(
            maquinaLinhaId,
            request.Critica,
            request.VelocidadeNominal,
            request.SobreVelocidade,
            request.MedeProducao,
            request.TempoDeteccaoParadaSegundos
        );
        if (resultado is null) return NotFound();
        return Ok(resultado);
    }

    private static string? ErroTempoDeteccao(int? segundos) =>
        segundos is < LimitesTempoDeteccaoParada.MinimoSegundos or > LimitesTempoDeteccaoParada.MaximoSegundos
            ? $"Tempo para detectar parada deve ficar entre {LimitesTempoDeteccaoParada.MinimoSegundos} e {LimitesTempoDeteccaoParada.MaximoSegundos} segundos."
            : null;

    [HttpDelete("{maquinaLinhaId}")]
    public async Task<IActionResult> Remover(Guid linhaId, Guid maquinaLinhaId)
    {
        var sucesso = await _service.RemoverMaquinaAsync(maquinaLinhaId);
        if (!sucesso) return NotFound();
        return NoContent();
    }

    [HttpPatch("reordenar")]
    public async Task<IActionResult> Reordenar(Guid linhaId, [FromBody] ReordenarMaquinasRequest request)
    {
        await _service.ReordenarAsync(linhaId, request.Itens);
        return NoContent();
    }
}

/// <param name="TempoDeteccaoParadaSegundos">Z do Semi Automático (10 a 3600 s); nulo = 60 s.</param>
public record AdicionarMaquinaLinhaRequest(Guid MaquinaId, bool Critica, decimal VelocidadeNominal, decimal SobreVelocidade, bool MedeProducao = true, int? TempoDeteccaoParadaSegundos = null);

/// <param name="TempoDeteccaoParadaSegundos">Z do Semi Automático (10 a 3600 s); nulo = mantém o atual.</param>
public record AtualizarMaquinaLinhaRequest(bool Critica, decimal VelocidadeNominal, decimal SobreVelocidade, bool MedeProducao = true, int? TempoDeteccaoParadaSegundos = null);
public record ReordenarMaquinasRequest(IList<ReordenarItem> Itens);