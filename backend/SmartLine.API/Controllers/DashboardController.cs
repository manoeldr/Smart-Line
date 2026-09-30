using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;
using SmartLine.Iot.Coleta;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ColetaIotService _coleta;

    public DashboardController(IDashboardService dashboardService, ColetaIotService coleta)
    {
        _dashboardService = dashboardService;
        _coleta = coleta;
    }

    [HttpGet("linhas/{linhaId}")]
    public async Task<IActionResult> GetDashboardLinha(Guid linhaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim)
    {
        var resultado = await _dashboardService.GetDashboardLinhaAsync(linhaId, inicio, fim);

        // Semi Automático: a situação ao vivo é a do motor da coleta (só existe em memória).
        return Ok(resultado.Select(m => m.AcompanhamentoId is null ? m : m with { SituacaoAoVivo = SituacaoColeta(m) }));
    }

    /// <summary>Situação vista pelo motor; antes da primeira mensagem do WISE, aguardando.</summary>
    private string SituacaoColeta(MaquinaDashboardDto maquina)
    {
        var situacao = Guid.TryParse(maquina.MaquinaLinhaId, out var maquinaLinhaId) ? _coleta.Situacao(maquinaLinhaId) : null;
        return situacao is not null && situacao.AcompanhamentoId.ToString() == maquina.AcompanhamentoId
            ? situacao.Situacao.ToString()
            : nameof(Core.Iot.SituacaoMaquina.AguardandoPrimeiraAmostra);
    }
}
