using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/configuracao")]
[Authorize]
public class ConfiguracaoController : ControllerBase
{
    private readonly IConfiguracaoService _service;

    public ConfiguracaoController(IConfiguracaoService service)
    {
        _service = service;
    }

    // ── Usuários ──────────────────────────────────────────────
    // Só Administrador e Desenvolvedor; o perfil Desenvolvedor é reservado ao Desenvolvedor
    // (ver IConfiguracaoService), e a recusa vem como 403 com mensagem para a tela.
    private bool ChamadorDesenvolvedor => User.FindFirst("nivel")?.Value == "Desenvolvedor";

    private ObjectResult Reservado(PerfilReservadoException e) =>
        StatusCode(StatusCodes.Status403Forbidden, new { mensagem = e.Message });

    [HttpGet("usuarios")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> GetUsuarios() =>
        Ok(await _service.GetUsuariosAsync(ChamadorDesenvolvedor));

    [HttpPost("usuarios")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioRequest req)
    {
        try { return Ok(await _service.CriarUsuarioAsync(req, ChamadorDesenvolvedor)); }
        catch (PerfilReservadoException e) { return Reservado(e); }
    }

    [HttpPut("usuarios/{id}")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> EditarUsuario(Guid id, [FromBody] EditarUsuarioRequest req)
    {
        try { return Ok(await _service.EditarUsuarioAsync(id, req, ChamadorDesenvolvedor)); }
        catch (PerfilReservadoException e) { return Reservado(e); }
    }

    [HttpDelete("usuarios/{id}")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> DeletarUsuario(Guid id)
    {
        try { await _service.DeletarUsuarioAsync(id, ChamadorDesenvolvedor); }
        catch (PerfilReservadoException e) { return Reservado(e); }
        return NoContent();
    }

    // ── Clientes ──────────────────────────────────────────────
    [HttpGet("clientes")]
    public async Task<IActionResult> GetClientes() =>
        Ok(await _service.GetClientesAsync());

    [HttpPost("clientes")]
    public async Task<IActionResult> CriarCliente([FromBody] CriarClienteRequest req) =>
        Ok(await _service.CriarClienteAsync(req));

    [HttpPut("clientes/{id}")]
    public async Task<IActionResult> EditarCliente(Guid id, [FromBody] EditarClienteRequest req) =>
        Ok(await _service.EditarClienteAsync(id, req));

    [HttpDelete("clientes/{id}")]
    public async Task<IActionResult> DeletarCliente(Guid id)
    {
        await _service.DeletarClienteAsync(id);
        return NoContent();
    }

    // ── Linhas ────────────────────────────────────────────────
    [HttpGet("linhas")]
    public async Task<IActionResult> GetLinhas() =>
        Ok(await _service.GetLinhasAsync());

    [HttpPost("linhas")]
    public async Task<IActionResult> CriarLinha([FromBody] CriarLinhaRequest req) =>
        Ok(await _service.CriarLinhaAsync(req));

    [HttpPut("linhas/{id}")]
    public async Task<IActionResult> EditarLinha(Guid id, [FromBody] EditarLinhaRequest req) =>
        Ok(await _service.EditarLinhaAsync(id, req));

    [HttpDelete("linhas/{id}")]
    public async Task<IActionResult> DeletarLinha(Guid id)
    {
        await _service.DeletarLinhaAsync(id);
        return NoContent();
    }

    // ── Máquinas ──────────────────────────────────────────────
    [HttpGet("maquinas")]
    public async Task<IActionResult> GetMaquinas() =>
        Ok(await _service.GetMaquinasAsync());

    [HttpPost("maquinas")]
    public async Task<IActionResult> CriarMaquina([FromBody] CriarMaquinaRequest req) =>
        Ok(await _service.CriarMaquinaAsync(req));

    [HttpPut("maquinas/{id}")]
    public async Task<IActionResult> EditarMaquina(Guid id, [FromBody] EditarMaquinaRequest req) =>
        Ok(await _service.EditarMaquinaAsync(id, req));

    [HttpDelete("maquinas/{id}")]
    public async Task<IActionResult> DeletarMaquina(Guid id)
    {
        await _service.DeletarMaquinaAsync(id);
        return NoContent();
    }

    // ── Campos de Máquina ─────────────────────────────────────
    [HttpGet("maquinas/{maquinaId}/campos")]
    public async Task<IActionResult> GetCamposMaquina(Guid maquinaId) =>
        Ok(await _service.GetCamposMaquinaAsync(maquinaId));

    [HttpPost("maquinas/{maquinaId}/campos")]
    public async Task<IActionResult> CriarCampoMaquina(Guid maquinaId, [FromBody] CriarCampoMaquinaRequest req) =>
        Ok(await _service.CriarCampoMaquinaAsync(maquinaId, req));

    [HttpPut("campos/{id}")]
    public async Task<IActionResult> EditarCampoMaquina(Guid id, [FromBody] EditarCampoMaquinaRequest req) =>
        Ok(await _service.EditarCampoMaquinaAsync(id, req));

    [HttpDelete("campos/{id}")]
    public async Task<IActionResult> DeletarCampoMaquina(Guid id)
    {
        await _service.DeletarCampoMaquinaAsync(id);
        return NoContent();
    }
}