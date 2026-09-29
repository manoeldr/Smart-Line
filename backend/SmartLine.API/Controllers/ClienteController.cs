using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClienteController : ControllerBase
{
    private readonly IClienteService _clienteService;
    private readonly ColetaIotService _coleta;
    private readonly BrokerMqttService _broker;

    public ClienteController(IClienteService clienteService, ColetaIotService coleta, BrokerMqttService broker)
    {
        _clienteService = clienteService;
        _coleta = coleta;
        _broker = broker;
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

    /// <summary>Situação vista pelo motor; antes da primeira mensagem do WISE, aguardando.</summary>
    private string SituacaoColeta(MaquinaLinhaOverviewDto maquina)
    {
        var situacao = Guid.TryParse(maquina.Id, out var maquinaLinhaId) ? _coleta.Situacao(maquinaLinhaId) : null;
        return situacao is not null && situacao.AcompanhamentoId.ToString() == maquina.AcompanhamentoId
            ? situacao.Situacao.ToString()
            : nameof(Core.Iot.SituacaoMaquina.AguardandoPrimeiraAmostra);
    }

    [HttpGet("{id}/linhas")]
    public async Task<IActionResult> GetLinhas(Guid id)
    {
        var linhas = await _clienteService.GetLinhasAsync(id);
        var conectados = _broker.IpsConectados();

        // O que só existe em memória (motor da coleta e broker) entra aqui, sobre o que veio do banco.
        return Ok(linhas.Select(l => l with
        {
            Maquinas = l.Maquinas.Select(m => m with
            {
                SituacaoColeta = m.AcompanhamentoId is null ? null : SituacaoColeta(m),
                WiseConectado = m.EnderecoIpWise is null ? null : conectados.Contains(m.EnderecoIpWise)
            }).ToList()
        }));
    }
}
