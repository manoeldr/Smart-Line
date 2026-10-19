using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Rede;

/// <summary>
/// Zera os contadores do WISE pela interface HTTP dele (a mesma da página de
/// configuração), para quem está em campo não precisar abrir o aparelho e
/// mexer em mais nada. Usado ao iniciar a medição Semi Automática.
/// </summary>
/// <remarks>
/// Confirmado na bancada em 19/10/2026 (captura da própria tela do WISE-4051):
/// <c>GET /di_value/slot_0/ch_N</c> (ch_0 = S1 … ch_7 = S8) devolve
/// <c>{"Ch":1,"Md":1,"Val":307,"Stat":0,"Cnting":1,"OvLch":0}</c> (Md 1 = contador), e o
/// comando de limpar é <c>PATCH /di_value/slot_0</c> com
/// <c>{"DIVal":[{"ClrCnt":1,"Ch":1}]}</c> — um item por canal. Gravar <c>Val: 0</c> por
/// PUT no canal responde 200 e não faz nada. Usuário e senha em Basic. Como o WISE
/// responde 200 mesmo ignorando, o valor é lido antes e depois: só é sucesso se caiu.
/// </remarks>
public interface IZeradorContadoresWise
{
    /// <param name="enderecoIp">IP já validado, na forma canônica.</param>
    /// <param name="contadores">Canais marcados como contador na medição (ex.: S2, S3).</param>
    Task<ResultadoZerarContadores> ZerarAsync(string enderecoIp, IReadOnlyCollection<CanalWise> contadores, CancellationToken cancellationToken = default);
}

/// <param name="Erro">Texto para a tela; nulo quando zerou todos.</param>
public sealed record ResultadoZerarContadores(bool Sucesso, string? Erro)
{
    public static ResultadoZerarContadores Ok { get; } = new(true, null);
    public static ResultadoZerarContadores Falha(string erro) => new(false, erro);
}

/// <summary>
/// Acesso HTTP aos WISE da instalação — o mesmo usuário e senha para todos
/// (o de fábrica é root / 00000000). Variáveis de ambiente:
/// WISE_HTTP_PORTA (80), WISE_HTTP_USUARIO (root), WISE_HTTP_SENHA (00000000).
/// </summary>
public sealed class OpcoesHttpWise
{
    public int Porta { get; init; } = 80;
    public string Usuario { get; init; } = "root";
    public string Senha { get; init; } = "00000000";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    public static OpcoesHttpWise LerDoAmbiente() => new()
    {
        Porta = int.TryParse(Environment.GetEnvironmentVariable("WISE_HTTP_PORTA"), out var porta) && porta is > 0 and <= 65535 ? porta : 80,
        Usuario = Environment.GetEnvironmentVariable("WISE_HTTP_USUARIO") is { Length: > 0 } usuario ? usuario : "root",
        Senha = Environment.GetEnvironmentVariable("WISE_HTTP_SENHA") ?? "00000000",
    };
}

/// <summary>
/// Antes de ir ao HTTP, vê se o IP é de um WISE simulado ligado dentro do backend: esse é
/// zerado direto, sem rede, e nunca falha (o simulador existe para testar o resto).
/// </summary>
public sealed class ZeradorComSimulador : IZeradorContadoresWise
{
    private readonly Simulacao.SimuladorWise _simulador;
    private readonly IZeradorContadoresWise _real;

    public ZeradorComSimulador(Simulacao.SimuladorWise simulador, IZeradorContadoresWise real)
    {
        _simulador = simulador;
        _real = real;
    }

    public async Task<ResultadoZerarContadores> ZerarAsync(string enderecoIp, IReadOnlyCollection<CanalWise> contadores, CancellationToken cancellationToken = default)
    {
        if (contadores.Count > 0 && await _simulador.ZerarContadoresAsync(enderecoIp, cancellationToken))
            return ResultadoZerarContadores.Ok;
        return await _real.ZerarAsync(enderecoIp, contadores, cancellationToken);
    }
}

public sealed class ZeradorContadoresWise : IZeradorContadoresWise, IDisposable
{
    private readonly OpcoesHttpWise _opcoes;
    private readonly HttpClient _http;

    public ZeradorContadoresWise(OpcoesHttpWise opcoes)
    {
        _opcoes = opcoes;
        _http = new HttpClient { Timeout = opcoes.Timeout };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opcoes.Usuario}:{opcoes.Senha}")));
    }

    /// <summary>Caminho do canal no WISE: S1 = ch_0 … S8 = ch_7.</summary>
    public static string Caminho(CanalWise canal) => $"/di_value/slot_0/ch_{(int)canal - 1}";

    /// <summary>Caminho do slot inteiro, onde vai o comando de limpar contadores.</summary>
    public const string CaminhoSlot = "/di_value/slot_0";

    /// <summary>Corpo do comando que a própria tela do WISE manda para limpar contadores (um item por canal).</summary>
    public static string CorpoLimparContadores(IEnumerable<CanalWise> contadores)
    {
        var itens = new JsonArray();
        foreach (var canal in contadores)
            itens.Add(new JsonObject { ["ClrCnt"] = 1, ["Ch"] = (int)canal - 1 });
        return new JsonObject { ["DIVal"] = itens }.ToJsonString();
    }

    public async Task<ResultadoZerarContadores> ZerarAsync(string enderecoIp, IReadOnlyCollection<CanalWise> contadores, CancellationToken cancellationToken = default)
    {
        if (contadores.Count == 0)
            return ResultadoZerarContadores.Ok;

        var baseUrl = $"http://{enderecoIp}:{_opcoes.Porta}";
        try
        {
            // Valores de antes, para conferir depois (o WISE responde 200 mesmo quando ignora).
            var antes = new Dictionary<CanalWise, long>();
            foreach (var canal in contadores)
                antes[canal] = ValorDe(await LerAsync(baseUrl + Caminho(canal), cancellationToken));
            if (antes.Values.All(v => v == 0))
                return ResultadoZerarContadores.Ok;

            using var pedido = new HttpRequestMessage(HttpMethod.Patch, baseUrl + CaminhoSlot)
            {
                Content = ConteudoJson(CorpoLimparContadores(contadores))
            };
            using var resposta = await _http.SendAsync(pedido, cancellationToken);
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                return ResultadoZerarContadores.Falha("O WISE recusou o usuário e a senha do Smart Line Scan.");
            if (!resposta.IsSuccessStatusCode)
            {
                var detalhe = await resposta.Content.ReadAsStringAsync(cancellationToken);
                return ResultadoZerarContadores.Falha(
                    $"O WISE não aceitou zerar os contadores (resposta {(int)resposta.StatusCode}{(string.IsNullOrWhiteSpace(detalhe) ? "" : ": " + detalhe.Trim())}).");
            }

            foreach (var (canal, valorAntes) in antes)
            {
                if (valorAntes == 0) continue;
                var depois = ValorDe(await LerAsync(baseUrl + Caminho(canal), cancellationToken));
                // Pode não ser 0 exato se entrou pulso no meio; o que importa é ter caído.
                if (depois >= valorAntes)
                    return ResultadoZerarContadores.Falha($"O WISE aceitou o comando, mas o contador {canal} continuou em {depois}.");
            }
            return ResultadoZerarContadores.Ok;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoZerarContadores.Falha($"O WISE não respondeu ao comando de zerar os contadores (porta {_opcoes.Porta}).");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ResultadoZerarContadores.Falha(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoZerarContadores.Falha($"Não foi possível falar com o WISE pela porta {_opcoes.Porta}: {ex.Message}");
        }
        catch (JsonException)
        {
            return ResultadoZerarContadores.Falha("O WISE respondeu num formato que o SmartLine não reconhece.");
        }
    }

    private async Task<JsonObject> LerAsync(string url, CancellationToken cancellationToken)
    {
        using var resposta = await _http.GetAsync(url, cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
            throw new HttpRequestException("O WISE recusou o usuário e a senha do Smart Line Scan.", null, HttpStatusCode.Unauthorized);
        resposta.EnsureSuccessStatusCode();
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return JsonNode.Parse(corpo) as JsonObject ?? new JsonObject();
    }

    private static long ValorDe(JsonObject registro) =>
        registro["Val"] is JsonValue v && v.TryGetValue<long>(out var n) ? n : 0;

    /// <summary>Content-Type sem "; charset=utf-8": firmware embarcado costuma comparar o cabeçalho inteiro.</summary>
    private static StringContent ConteudoJson(string json)
    {
        var conteudo = new StringContent(json, Encoding.UTF8);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return conteudo;
    }

    public void Dispose() => _http.Dispose();
}
