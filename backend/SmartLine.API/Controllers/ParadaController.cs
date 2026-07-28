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

    [HttpPost("{id}/foto")]
    public async Task<IActionResult> UploadFoto(Guid id, IFormFile foto)
    {
        var extensao = Path.GetExtension(foto.FileName);
        if (string.IsNullOrEmpty(extensao)) extensao = ".jpg";

        await using var stream = foto.OpenReadStream();
        var parada = await _paradaService.SalvarFotoAsync(id, stream, extensao);
        if (parada is null) return NotFound();
        return Ok(parada);
    }

    // Serve o arquivo físico da foto a partir do caminho relativo salvo em FotoPath.
    // Rota com {*caminho} (catch-all) porque o caminho contém uma barra (pasta/arquivo).
    [HttpGet("foto/{*caminho}")]
    public IActionResult ObterFoto(string caminho)
    {
        var raizFotos = Path.Combine(AppContext.BaseDirectory, "ImagesStopReason");
        var caminhoCompleto = Path.GetFullPath(Path.Combine(raizFotos, caminho));

        // Proteção contra path traversal: garante que o caminho final ainda está dentro da raiz de fotos
        if (!caminhoCompleto.StartsWith(Path.GetFullPath(raizFotos), StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (!System.IO.File.Exists(caminhoCompleto))
            return NotFound();

        var extensao = Path.GetExtension(caminhoCompleto).ToLowerInvariant();
        var contentType = extensao switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bytes = System.IO.File.ReadAllBytes(caminhoCompleto);
        return File(bytes, contentType);
    }
}

public record AbrirParadaRequest(Guid SessaoId, DateTime Inicio);
public record FecharParadaRequest(Guid MotivoId, DateTime Fim);