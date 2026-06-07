using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClienteController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClienteController(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var clientes = await _clienteService.GetAllAsync();
        return Ok(clientes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente is null) return NotFound();
        return Ok(cliente);
    }

    [HttpGet("{id}/linhas")]
    public async Task<IActionResult> GetLinhas(Guid id)
    {
        var linhas = await _clienteService.GetLinhasAsync(id);
        return Ok(linhas);
    }
}
