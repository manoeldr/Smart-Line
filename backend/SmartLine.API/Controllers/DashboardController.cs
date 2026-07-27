using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("linhas/{linhaId}")]
    public async Task<IActionResult> GetDashboardLinha(Guid linhaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        var resultado = await _dashboardService.GetDashboardLinhaAsync(linhaId, inicio, fim);
        return Ok(resultado);
    }
}