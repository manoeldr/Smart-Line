using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

/// <summary>
/// Folha timbrada do relatório (Configurações → Relatório). Ver e usar: qualquer usuário logado
/// (a tela de Relatórios precisa dela); importar e ajustar margens: Administrador e Desenvolvedor; remover: só Desenvolvedor.
/// </summary>
[ApiController]
[Route("api/configuracao/modelo-relatorio")]
[Authorize]
public class ModeloRelatorioController : ControllerBase
{
    private readonly IModeloRelatorioService _service;

    public ModeloRelatorioController(IModeloRelatorioService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult Obter() => Ok(_service.Obter());

    // A imagem da folha. Cache longo: a URL leva ?v=versao, que muda a cada importação.
    [HttpGet("imagem")]
    public IActionResult Imagem()
    {
        var imagem = _service.LerImagem();
        if (imagem is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(imagem.Value.Bytes, imagem.Value.ContentType);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> Importar(IFormFile arquivo)
    {
        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);
        try { return Ok(await _service.ImportarAsync(stream.ToArray(), arquivo.FileName)); }
        catch (ModeloRelatorioInvalidoException e) { return BadRequest(new { mensagem = e.Message }); }
    }

    [HttpPut("margens")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public IActionResult AtualizarMargens([FromBody] MargensRelatorioDto margens)
    {
        try { return Ok(_service.AtualizarMargens(margens)); }
        catch (ModeloRelatorioInvalidoException e) { return BadRequest(new { mensagem = e.Message }); }
    }

    [HttpDelete]
    [Authorize(Policy = Politicas.Desenvolvedor)]
    public IActionResult Remover()
    {
        _service.Remover();
        return NoContent();
    }
}
