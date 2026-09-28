using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;

namespace SmartLine.API.Controllers;

/// <summary>
/// Coleta Semi Automática (WISE): ligar e desligar por máquina e acompanhar ao vivo.
/// </summary>
[ApiController]
[Route("api/coleta-iot")]
[Authorize]
public class ColetaIotController : ControllerBase
{
    private readonly IAcompanhamentoService _acompanhamentos;
    private readonly ColetaIotService _coleta;
    private readonly BrokerMqttService _broker;

    public ColetaIotController(IAcompanhamentoService acompanhamentos, ColetaIotService coleta, BrokerMqttService broker)
    {
        _acompanhamentos = acompanhamentos;
        _coleta = coleta;
        _broker = broker;
    }

    /// <summary>Os oito canais do WISE, para montar a tela de iniciar (o mapa é fixo).</summary>
    [HttpGet("canais")]
    public IActionResult Canais() => Ok(MapaWise.Canais.Select(c => new CanalWiseDto(
        c.Canal, c.Entrada, c.Tipo, c.Funcao, c.Descricao, c.EhContadorProducao)));

    /// <summary>Todas as coletas ligadas, com o estado ao vivo de cada máquina.</summary>
    [HttpGet]
    public async Task<IActionResult> Painel(CancellationToken cancellationToken)
    {
        var conectados = _broker.IpsConectados();
        var coletas = await _acompanhamentos.ListarEmAndamentoAsync(null, cancellationToken);
        return Ok(coletas.Select(c => Montar(c, conectados)));
    }

    /// <summary>Coleta ligada numa máquina; 404 se a máquina não está em coleta.</summary>
    [HttpGet("maquina/{maquinaLinhaId:guid}")]
    public async Task<IActionResult> DaMaquina(Guid maquinaLinhaId, CancellationToken cancellationToken)
    {
        var coleta = (await _acompanhamentos.ListarEmAndamentoAsync(maquinaLinhaId, cancellationToken)).SingleOrDefault();
        return coleta is null ? NotFound() : Ok(Montar(coleta, _broker.IpsConectados()));
    }

    /// <summary>
    /// Liga a coleta numa máquina. Uma por chamada; várias podem rodar ao mesmo
    /// tempo. O motor começa a acompanhar em até um segundo.
    /// </summary>
    [HttpPost("iniciar")]
    public async Task<IActionResult> Iniciar([FromBody] IniciarAcompanhamentoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _acompanhamentos.IniciarAsync(ObterUsuarioId(), request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Iniciado) : BadRequest(new { mensagem = resultado.Erro });
    }

    /// <summary>
    /// Finaliza a coleta gravando antes a produção pendente. Quem iniciou, ou
    /// Administrador/Desenvolvedor.
    /// </summary>
    [HttpPost("{acompanhamentoId:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid acompanhamentoId, CancellationToken cancellationToken)
    {
        var nivel = User.FindFirst("nivel")?.Value;
        var podeFinalizarDeOutros = nivel is "Administrador" or "Desenvolvedor";

        var resultado = await _coleta.FinalizarAsync(acompanhamentoId, ObterUsuarioId(), podeFinalizarDeOutros, cancellationToken);
        return resultado switch
        {
            ResultadoFinalizacao.Finalizado => NoContent(),
            ResultadoFinalizacao.NaoEncontrado => NotFound(),
            ResultadoFinalizacao.JaFinalizado => Conflict(new { mensagem = "Esta coleta já foi finalizada." }),
            ResultadoFinalizacao.SemPermissao => StatusCode(StatusCodes.Status403Forbidden,
                new { mensagem = "Só quem iniciou a coleta, um Administrador ou um Desenvolvedor pode finalizá-la." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── Apoio ───────────────────────────────────────────────────────

    private ColetaIotPainelDto Montar(ColetaIotResumoDto coleta, IReadOnlySet<string> conectados)
    {
        // Nulo se o motor ainda não pegou esta coleta (acabou de ser iniciada).
        var situacao = _coleta.Situacao(coleta.MaquinaLinhaId);
        var aoVivo = situacao?.AcompanhamentoId == coleta.AcompanhamentoId ? situacao : null;

        var lidos = coleta.Canais.Select(c => c.Canal).ToHashSet();
        IReadOnlyList<SensorAoVivoDto> sensores = aoVivo is null
            ? []
            : MapaWise.Canais
                .Where(d => d.Tipo == TipoCanal.Estado && lidos.Contains(d.Canal) && aoVivo.Sensores.ContainsKey(d.Canal))
                .Select(d => new SensorAoVivoDto(d.Canal, d.Descricao, aoVivo.Sensores[d.Canal], d.EstaEmAlarme(aoVivo.Sensores[d.Canal])))
                .ToList();

        return new ColetaIotPainelDto(
            coleta,
            aoVivo?.Situacao,
            aoVivo?.UltimaAmostraUtc,
            coleta.ProducaoConsolidada + (aoVivo?.ProducaoPendente.Garrafas ?? 0),
            coleta.RefugoConsolidado + (aoVivo?.ProducaoPendente.Rejeito ?? 0),
            sensores,
            aoVivo?.Contadores ?? new Dictionary<CanalWise, uint>(),
            coleta.EnderecoIp is not null && conectados.Contains(coleta.EnderecoIp));
    }

    private Guid ObterUsuarioId() => Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Usuário não autenticado"));
}

public record CanalWiseDto(CanalWise Canal, int Entrada, TipoCanal Tipo, FuncaoCanal Funcao, string Descricao, bool ContadorDeProducao);

/// <param name="Situacao">Nulo enquanto o motor não pegou a coleta (no primeiro segundo depois de iniciar).</param>
/// <param name="ProducaoSessao">Garrafas da sessão do dia: consolidadas + apuradas e ainda não gravadas.</param>
/// <param name="Sensores">Sensores de estado marcados na medição, com o último valor e se está em alarme.</param>
/// <param name="Contadores">Último valor bruto de cada contador, como veio do WISE.</param>
/// <param name="WiseConectado">Conexão MQTT aberta agora.</param>
public record ColetaIotPainelDto(
    ColetaIotResumoDto Coleta,
    SituacaoMaquina? Situacao,
    DateTime? UltimaMensagem,
    long ProducaoSessao,
    long RefugoSessao,
    IReadOnlyList<SensorAoVivoDto> Sensores,
    IReadOnlyDictionary<CanalWise, uint> Contadores,
    bool WiseConectado);

/// <param name="ValorBruto">Como veio do WISE (sensores invertidos: true = sem presença).</param>
public record SensorAoVivoDto(CanalWise Canal, string Descricao, bool ValorBruto, bool EmAlarme);
