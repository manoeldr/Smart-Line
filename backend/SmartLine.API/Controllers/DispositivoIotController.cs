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
/// Cadastro dos WISE (a lista de aparelhos que podem ser usados numa medição)
/// e diagnóstico da comunicação (entradas ao vivo, ping, broker). O WISE não é
/// de nenhuma máquina: é escolhido ao iniciar a medição e fica livre ao finalizar.
/// </summary>
[ApiController]
[Route("api/dispositivos-iot")]
[Authorize]
public class DispositivoIotController : ControllerBase
{
    private readonly IWiseService _wises;
    private readonly ILocalizadorColetaIot _localizador;
    private readonly IEntradasWiseService _entradas;
    private readonly BrokerMqttService _broker;
    private readonly OpcoesBrokerMqtt _opcoesBroker;
    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly ColetaIotService _coleta;
    private readonly ITestePing _ping;

    public DispositivoIotController(
        IWiseService wises,
        ILocalizadorColetaIot localizador,
        IEntradasWiseService entradas,
        BrokerMqttService broker,
        OpcoesBrokerMqtt opcoesBroker,
        CaixaDeEntradaMqtt caixa,
        ColetaIotService coleta,
        ITestePing ping)
    {
        _wises = wises;
        _localizador = localizador;
        _entradas = entradas;
        _broker = broker;
        _opcoesBroker = opcoesBroker;
        _caixa = caixa;
        _coleta = coleta;
        _ping = ping;
    }

    /// <summary>
    /// Todos os WISE que se sabe existir: cadastrados, conectados, que
    /// publicaram desde que o backend subiu, ou em medição. Também serve o
    /// Configurar medição (escolha do WISE), por isso Operação.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Politicas.Operacao)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(ListaWise.Montar(
            await _wises.ListarAsync(cancellationToken),
            _broker.Conexoes(),
            _coleta.WiseVistos(),
            await _localizador.WisesEmMedicaoAsync(cancellationToken)));

    /// <summary>Cadastra um WISE (o ping antes é feito pela tela).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> Adicionar([FromBody] SalvarWiseRequest request, CancellationToken cancellationToken) =>
        Responder(await _wises.AdicionarAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> Editar(Guid id, [FromBody] SalvarWiseRequest request, CancellationToken cancellationToken) =>
        Responder(await _wises.EditarAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Politicas.AdministradorOuDesenvolvedor)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _wises.RemoverAsync(id, cancellationToken);
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return NoContent();
    }

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

    private IActionResult Responder(ResultadoCadastro<WiseCadastradoDto> resultado)
    {
        if (resultado.NaoEncontrado) return NotFound();
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Valor);
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
