using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/export-import")]
[Authorize]
public class ExportImportController : ControllerBase
{
    private readonly IExportImportService _service;

    public ExportImportController(IExportImportService service)
    {
        _service = service;
    }

    [HttpPost("exportar")]
    public async Task<IActionResult> Exportar([FromBody] ExportOpcoes opcoes)
    {
        var zipBytes = await _service.ExportarAsync(opcoes);
        var nomeArquivo = $"smartline-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return File(zipBytes, "application/zip", nomeArquivo);
    }

    [HttpPost("previa")]
    public async Task<IActionResult> Previa(IFormFile arquivo)
    {
        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);
        var resumo = await _service.PreviaImportacaoAsync(stream.ToArray());
        return Ok(resumo);
    }

    [HttpPost("importar")]
    public async Task<IActionResult> Importar(IFormFile arquivo)
    {
        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);
        var resumo = await _service.ImportarAsync(stream.ToArray());
        return Ok(resumo);
    }
}