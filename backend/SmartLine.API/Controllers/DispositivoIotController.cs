using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;
using SmartLine.Iot.Rede;

namespace SmartLine.API.Controllers;

/// <summary>
/// WISE vistos pelo SmartLine e diagnóstico da comunicação (entradas ao vivo,
/// ping, broker). Não há cadastro: o WISE é associado a uma máquina ao iniciar
/// a medição e fica livre ao finalizar.
/// </summary>
[ApiController]
[Route("api/dispositivos-iot")]
[Authorize]
public class DispositivoIotController : ControllerBase
{
    private readonly ILocalizadorColetaIot _localizador;
    private readonly IEntradasWiseService _entradas;
    private readonly BrokerMqttService _broker;
    private readonly OpcoesBrokerMqtt _opcoesBroker;
    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly ColetaIotService _coleta;
    private readonly ITestePing _ping;

    public DispositivoIotController(
        ILocalizadorColetaIot localizador,
        IEntradasWiseService entradas,
        BrokerMqttService broker,
        OpcoesBrokerMqtt opcoesBroker,
        CaixaDeEntradaMqtt caixa,
        ColetaIotService coleta,
        ITestePing ping)
    {
        _localizador = localizador;
        _entradas = entradas;
        _broker = broker;
        _opcoesBroker = opcoesBroker;
        _caixa = caixa;
        _coleta = coleta;
        _ping = ping;
    }

    /// <summary>
    /// Todos os WISE que se sabe existir: conectados, que publicaram desde que
    /// o backend subiu, ou em medição. Também serve o Configurar medição
    /// (situação do IP digitado e sugestões de WISE livres), por isso Operação.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(ListaWise.Montar(_broker.Conexoes(), _coleta.WiseVistos(), await _localizador.WisesEmMedicaoAsync(cancellationToken)));

    /// <summary>
    /// Validar entradas de um IP: as 8 entradas ao vivo, em medição ou livre.
    /// Em medição, com os textos da máquina; livre, com os padrão.
    /// </summary>
    [HttpGet("entradas")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> Entradas([FromQuery] string ip, CancellationToken cancellationToken)
    {
        var enderecoIp = EnderecoRede.Normalizar(ip);
        if (enderecoIp is null)
            return BadRequest(new { mensagem = $"IP inválido: \"{ip}\"." });

        var medicao = await _localizador.WiseEmMedicaoAsync(enderecoIp, cancellationToken);
        var textos = medicao is null
            ? EntradasAoVivo.TextosPadrao()
            : await _entradas.ObterAsync(medicao.MaquinaId, cancellationToken) ?? EntradasAoVivo.TextosPadrao();
        var leitura = _coleta.LeiturasDoWise(enderecoIp);

        return Ok(new EntradasDoWiseDto(
            enderecoIp,
            _broker.IpsConectados().Contains(enderecoIp),
            ListaWise.MaisRecente(leitura?.UltimaMensagemUtc, medicao?.UltimaMensagemEmUtc),
            medicao is null ? null : MedicaoDoWiseDto.De(medicao),
            EntradasAoVivo.Montar(textos, leitura)));
    }

    /// <summary>
    /// Ping até o IP (4 pacotes, como o do Windows), a partir do PC do
    /// SmartLine. Responde mas não conecta no broker = configuração MQTT do
    /// WISE; não responde = energia, cabo, rede ou IP errado.
    /// </summary>
    [HttpPost("ping")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> Ping([FromBody] PingRequest request, CancellationToken cancellationToken)
    {
        var enderecoIp = EnderecoRede.Normalizar(request.EnderecoIp);
        if (enderecoIp is null)
            return BadRequest(new { mensagem = $"IP inválido: \"{request.EnderecoIp}\"." });

        return Ok(await _ping.PingarAsync(enderecoIp, cancellationToken));
    }

    /// <summary>Situação do broker e do motor da coleta, para a tela de diagnóstico.</summary>
    [HttpGet("status")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public IActionResult Status() => Ok(new StatusColetaIotDto(
        _opcoesBroker.Habilitado,
        _broker.EmExecucao,
        _opcoesBroker.Porta,
        _broker.IpsConectados().Count,
        _coleta.MensagensProcessadas,
        _coleta.MensagensDescartadas,
        _caixa.Descartadas));
}

/// <param name="EnderecoIp">Como digitado (ex.: "192.168.10.21").</param>
public record PingRequest(string? EnderecoIp);

/// <param name="MensagensDescartadas">Não viraram amostra: WISE livre (sem medição), formato inválido...</param>
/// <param name="MensagensPerdidasFilaCheia">Perdidas na entrada por fila cheia (consumo travado). Deveria ser sempre 0.</param>
public record StatusColetaIotDto(
    bool BrokerHabilitado,
    bool BrokerEmExecucao,
    int Porta,
    int WiseConectados,
    long MensagensProcessadas,
    long MensagensDescartadas,
    long MensagensPerdidasFilaCheia);
