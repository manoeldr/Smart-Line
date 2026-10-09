using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLine.API.Autorizacao;
using SmartLine.Core.Interfaces;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;
using SmartLine.Iot.Diagnostico;
using SmartLine.Iot.Simulacao;

namespace SmartLine.API.Controllers;

/// <summary>
/// Área do Desenvolvedor: o log do backend e o simulador de WISE, dentro do sistema,
/// sem terminal. Só o nível Desenvolvedor enxerga.
/// </summary>
[ApiController]
[Route("api/dev")]
[Authorize(Policy = Politicas.Desenvolvedor)]
public class DesenvolvedorController : ControllerBase
{
    private readonly LogEmMemoria _log;
    private readonly SimuladorWise _simulador;
    private readonly OpcoesBrokerMqtt _broker;
    private readonly IWiseService _wises;
    private readonly ColetaIotService _coleta;

    public DesenvolvedorController(LogEmMemoria log, SimuladorWise simulador, OpcoesBrokerMqtt broker, IWiseService wises, ColetaIotService coleta)
    {
        _log = log;
        _simulador = simulador;
        _broker = broker;
        _wises = wises;
        _coleta = coleta;
    }

    // ── Logs ──────────────────────────────────────────────────────────────

    public sealed record LinhaLogDto(long Sequencia, DateTime Instante, string Nivel, string Categoria, string Mensagem, string? Excecao);
    public sealed record LogsDto(long UltimaSequencia, int Capacidade, IReadOnlyList<LinhaLogDto> Linhas);

    /// <summary>
    /// Últimas linhas do log desde que o backend subiu. A tela chama de novo com
    /// <paramref name="depoisDe"/> = a última sequência que já tem, e recebe só o que é novo.
    /// </summary>
    /// <param name="nivel">Nível mínimo: Information, Warning, Error (padrão: tudo).</param>
    /// <param name="texto">Trecho a procurar na mensagem, na categoria ou na exceção.</param>
    [HttpGet("logs")]
    public ActionResult<LogsDto> Logs([FromQuery] long depoisDe = 0, [FromQuery] string? nivel = null, [FromQuery] string? texto = null, [FromQuery] int maximo = 500)
    {
        LogLevel? nivelMinimo = null;
        if (!string.IsNullOrWhiteSpace(nivel))
        {
            if (!Enum.TryParse<LogLevel>(nivel, ignoreCase: true, out var n))
                return BadRequest(new { mensagem = "Nível de log desconhecido." });
            nivelMinimo = n;
        }

        var linhas = _log.Linhas(depoisDe, nivelMinimo, texto, Math.Clamp(maximo, 1, LogEmMemoria.CapacidadePadrao))
            .Select(l => new LinhaLogDto(l.Sequencia, l.InstanteUtc, l.Nivel.ToString(), SemNamespace(l.Categoria), l.Mensagem, l.Excecao))
            .ToList();
        return new LogsDto(_log.UltimaSequencia, LogEmMemoria.CapacidadePadrao, linhas);
    }

    private static string SemNamespace(string categoria)
    {
        var ponto = categoria.LastIndexOf('.');
        return ponto >= 0 ? categoria[(ponto + 1)..] : categoria;
    }

    // ── Simulador ─────────────────────────────────────────────────────────

    /// <summary>O que o Desenvolvedor escolhe na tela ao ligar o simulador (tempos em minutos).</summary>
    public sealed record LigarSimuladorRequest(
        int Maquinas = 2,
        int IntervaloSegundos = 20,
        double[]? GarrafasPorMinuto = null,
        double[]? GarrafasPorPulso = null,
        double RejeitoPercentual = 1,
        int PrimeiroIp = 21,
        bool Automatico = true,
        double RodandoMedioMin = 10,
        double ParadaMinimaMin = 1,
        double ParadaMaximaMin = 5,
        bool Quedas = true,
        double QuedaMediaMin = 60,
        double QuedaMinimaMin = 3,
        double QuedaMaximaMin = 10,
        int PortaHttp = 80);

    public sealed record MaquinaSimuladaDto(
        int Numero, string EnderecoIp, string Cenario, string? Falha, uint ContadorProducao, uint ContadorRejeito,
        bool Conectada, bool Automatico, DateTime? ProximaMudanca, DateTime? ProximaMudancaComunicacao,
        double GarrafasPorMinuto, double GarrafasPorPulso, bool InterfaceHttp);

    public sealed record SimuladorDto(bool Ligado, DateTime? LigadoEm, int PortaBroker, LigarSimuladorRequest? Opcoes, IReadOnlyList<MaquinaSimuladaDto> Maquinas, string? Aviso = null);

    [HttpGet("simulador")]
    public ActionResult<SimuladorDto> Simulador() => Situacao();

    [HttpPost("simulador/ligar")]
    public async Task<ActionResult<SimuladorDto>> Ligar([FromBody] LigarSimuladorRequest pedido, CancellationToken cancellationToken)
    {
        if (pedido.Maquinas is < 1 or > 50)
            return BadRequest(new { mensagem = "Simule de 1 a 50 máquinas." });
        if (pedido.IntervaloSegundos is < 1 or > 3600)
            return BadRequest(new { mensagem = "O intervalo de publicação vai de 1 s a 1 h." });
        if (pedido.PortaHttp is < 0 or > 65535)
            return BadRequest(new { mensagem = "Porta HTTP inválida." });

        static TimeSpan Minutos(double v, double minimo) => TimeSpan.FromMinutes(Math.Clamp(v, minimo, 1440));
        var paradas = new OpcoesParadasAleatorias(
            Minutos(pedido.RodandoMedioMin, 0.1),
            Minutos(pedido.ParadaMinimaMin, 0.1),
            Minutos(Math.Max(pedido.ParadaMaximaMin, pedido.ParadaMinimaMin), 0.1),
            pedido.Quedas,
            Minutos(pedido.QuedaMediaMin, 0.5),
            Minutos(pedido.QuedaMinimaMin, 0.1),
            Minutos(Math.Max(pedido.QuedaMaximaMin, pedido.QuedaMinimaMin), 0.1));

        var opcoes = new OpcoesSimulador(
            pedido.Maquinas,
            pedido.IntervaloSegundos,
            Positivos(pedido.GarrafasPorMinuto, 600),
            Positivos(pedido.GarrafasPorPulso, 1),
            Math.Clamp(pedido.RejeitoPercentual, 0, 100),
            "127.0.0.1",
            _broker.Porta,
            pedido.PrimeiroIp,
            pedido.Automatico,
            paradas,
            pedido.PortaHttp);

        await _simulador.LigarAsync(opcoes, cancellationToken);
        await CadastrarSimuladosAsync(cancellationToken);
        return Situacao();
    }

    /// <summary>
    /// Os WISE simulados entram no cadastro ao ligar (nome "Simulado N") e saem ao desligar, para
    /// aparecerem na lista da medição sem cadastro à mão e não ficarem confundindo depois.
    /// Saem mesmo em medição: a medição guarda o IP e segue como "sem comunicação", como se o
    /// aparelho tivesse sido desligado.
    /// </summary>
    private async Task CadastrarSimuladosAsync(CancellationToken cancellationToken)
    {
        var cadastrados = (await _wises.ListarAsync(cancellationToken)).Select(w => w.EnderecoIp).ToHashSet();
        foreach (var m in _simulador.Situacao())
        {
            if (cadastrados.Contains(m.EnderecoIp)) continue;
            await _wises.AdicionarAsync(new SalvarWiseRequest(m.EnderecoIp, $"Simulado {m.Numero}"), cancellationToken);
        }
    }

    private async Task DescadastrarSimuladosAsync()
    {
        var ips = _simulador.Situacao().Select(m => m.EnderecoIp).ToHashSet();
        foreach (var w in (await _wises.ListarAsync()).Where(w => ips.Contains(w.EnderecoIp)))
            await _wises.RemoverMesmoEmMedicaoAsync(w.Id);
    }

    private static List<double> Positivos(double[]? valores, double padrao) =>
        valores is null || valores.Length == 0 ? [padrao] : valores.Select(v => v > 0 ? v : padrao).ToList();

    [HttpPost("simulador/desligar")]
    public async Task<ActionResult<SimuladorDto>> Desligar()
    {
        var ips = _simulador.Situacao().Select(m => m.EnderecoIp).ToList();
        await DescadastrarSimuladosAsync();
        await _simulador.DesligarAsync();
        // Também somem de "publicando sem cadastro" e do Validar entradas.
        foreach (var ip in ips)
            _coleta.EsquecerWise(ip);
        return Situacao();
    }

    public sealed record ComandoRequest(string Acao);

    /// <summary>Ação numa máquina (1..N) ou em todas (0): rodando, falta, minimo, saida, caixas, parada, desligar, semrede, reiniciar, auto, manual.</summary>
    [HttpPost("simulador/maquinas/{numero:int}/comando")]
    public async Task<ActionResult<SimuladorDto>> Comando(int numero, [FromBody] ComandoRequest pedido, CancellationToken cancellationToken)
    {
        if (!_simulador.Ligado)
            return BadRequest(new { mensagem = "O simulador está desligado." });
        if (!Enum.TryParse<AcaoSimulada>(pedido.Acao, ignoreCase: true, out var acao))
            return BadRequest(new { mensagem = $"Ação '{pedido.Acao}' desconhecida." });

        try
        {
            await _simulador.ComandoAsync(numero, acao, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
        return Situacao();
    }

    private SimuladorDto Situacao()
    {
        var o = _simulador.Opcoes;
        var p = o?.Paradas;
        var opcoes = o is null ? null : new LigarSimuladorRequest(
            o.Maquinas, o.IntervaloSegundos, o.GarrafasPorMinuto?.ToArray(), o.GarrafasPorPulso?.ToArray(), o.RejeitoPercentual,
            o.PrimeiroIp, o.Automatico,
            p?.RodandoMedio.TotalMinutes ?? 10, p?.ParadaMinima.TotalMinutes ?? 1, p?.ParadaMaxima.TotalMinutes ?? 5,
            p?.IncluirDesligado ?? true, p?.EntreQuedas.TotalMinutes ?? 60, p?.QuedaMin.TotalMinutes ?? 3, p?.QuedaMax.TotalMinutes ?? 10,
            o.PortaHttp);

        var maquinas = _simulador.Situacao().Select(m => new MaquinaSimuladaDto(
            m.Numero, m.EnderecoIp, m.Cenario.ToString(), m.Falha?.ToString(), m.ContadorProducao, m.ContadorRejeito,
            m.Conectada, m.Automatico, m.ProximaMudancaUtc, m.ProximaMudancaComunicacaoUtc,
            m.GarrafasPorMinuto, m.GarrafasPorPulso, m.InterfaceHttp)).ToList();

        return new SimuladorDto(_simulador.Ligado, _simulador.LigadoEmUtc, _broker.Porta, opcoes, maquinas);
    }
}
