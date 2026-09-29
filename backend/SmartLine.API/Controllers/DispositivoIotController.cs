using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;

namespace SmartLine.API.Controllers;

/// <summary>Cadastro dos WISE (Semi Automático) e diagnóstico do broker.</summary>
[ApiController]
[Route("api/dispositivos-iot")]
[Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
public class DispositivoIotController : ControllerBase
{
    private readonly IDispositivoIotService _service;
    private readonly BrokerMqttService _broker;
    private readonly OpcoesBrokerMqtt _opcoesBroker;
    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly ColetaIotService _coleta;

    public DispositivoIotController(
        IDispositivoIotService service,
        BrokerMqttService broker,
        OpcoesBrokerMqtt opcoesBroker,
        CaixaDeEntradaMqtt caixa,
        ColetaIotService coleta)
    {
        _service = service;
        _broker = broker;
        _opcoesBroker = opcoesBroker;
        _caixa = caixa;
        _coleta = coleta;
    }

    /// <summary>WISE cadastrados, com a conexão ao broker agora.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var conectados = _broker.IpsConectados();
        var lista = await _service.ListarAsync(cancellationToken);
        return Ok(lista.Select(d => d with { Conectado = conectados.Contains(d.EnderecoIp) }));
    }

    /// <summary>IPs publicando no broker sem WISE cadastrado: candidatos ao cadastro.</summary>
    [HttpGet("desconhecidos")]
    public IActionResult Desconhecidos() => Ok(_coleta.WiseDesconhecidos());

    /// <summary>Situação do broker e do motor da coleta, para a tela de diagnóstico.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new StatusColetaIotDto(
        _opcoesBroker.Habilitado,
        _broker.EmExecucao,
        _opcoesBroker.Porta,
        _broker.IpsConectados().Count,
        _coleta.MensagensProcessadas,
        _coleta.MensagensDescartadas,
        _caixa.Descartadas));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarDispositivoIotRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.CriarAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] SalvarDispositivoIotRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.EditarAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _service.ExcluirAsync(id, cancellationToken);
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return NoContent();
    }

    private IActionResult Responder(ResultadoCadastro<DispositivoIotDto> resultado)
    {
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });

        var dispositivo = resultado.Valor!;
        return Ok(dispositivo with { Conectado = _broker.IpsConectados().Contains(dispositivo.EnderecoIp) });
    }
}

/// <param name="MensagensDescartadas">Não viraram amostra: IP sem cadastro, máquina sem coleta, formato inválido...</param>
/// <param name="MensagensPerdidasFilaCheia">Perdidas na entrada por fila cheia (consumo travado). Deveria ser sempre 0.</param>
public record StatusColetaIotDto(
    bool BrokerHabilitado,
    bool BrokerEmExecucao,
    int Porta,
    int WiseConectados,
    long MensagensProcessadas,
    long MensagensDescartadas,
    long MensagensPerdidasFilaCheia);
