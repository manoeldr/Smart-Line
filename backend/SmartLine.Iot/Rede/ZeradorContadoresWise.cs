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
/// O WISE-4000 expõe cada entrada em <c>/di_value/slot_0/ch_N</c> (ch_0 = S1 …
/// ch_7 = S8) com usuário e senha (Basic). Lê o registro do canal, põe o valor
/// em zero e grava de volta — assim os demais campos do canal ficam como estavam.
/// O formato exato do registro só se confirma na bancada; se o WISE recusar, o
/// erro traz a resposta dele para ajustar.
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

    public async Task<ResultadoZerarContadores> ZerarAsync(string enderecoIp, IReadOnlyCollection<CanalWise> contadores, CancellationToken cancellationToken = default)
    {
        if (contadores.Count == 0)
            return ResultadoZerarContadores.Ok;

        var baseUrl = $"http://{enderecoIp}:{_opcoes.Porta}";
        foreach (var canal in contadores)
        {
            try
            {
                var url = baseUrl + Caminho(canal);

                // Lê o registro do canal para gravar de volta só com o valor zerado.
                JsonObject registro;
                using (var resposta = await _http.GetAsync(url, cancellationToken))
                {
                    if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                        return ResultadoZerarContadores.Falha("O WISE recusou o usuário e a senha do SmartLine.");
                    resposta.EnsureSuccessStatusCode();
                    var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
                    registro = JsonNode.Parse(corpo) as JsonObject ?? new JsonObject();
                }

                registro["Ch"] = (int)canal - 1;
                registro["Val"] = 0;
                if (registro.ContainsKey("Ov")) registro["Ov"] = 0; // flag de estouro do contador

                using var conteudo = new StringContent(registro.ToJsonString(), Encoding.UTF8, "application/json");
                using var gravacao = await _http.PutAsync(url, conteudo, cancellationToken);
                if (gravacao.StatusCode == HttpStatusCode.Unauthorized)
                    return ResultadoZerarContadores.Falha("O WISE recusou o usuário e a senha do SmartLine.");
                if (!gravacao.IsSuccessStatusCode)
                {
                    var detalhe = await gravacao.Content.ReadAsStringAsync(cancellationToken);
                    return ResultadoZerarContadores.Falha(
                        $"O WISE não aceitou zerar o contador {canal} (resposta {(int)gravacao.StatusCode}{(string.IsNullOrWhiteSpace(detalhe) ? "" : ": " + detalhe.Trim())}).");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ResultadoZerarContadores.Falha($"O WISE não respondeu ao comando de zerar os contadores (porta {_opcoes.Porta}).");
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

        return ResultadoZerarContadores.Ok;
    }

    public void Dispose() => _http.Dispose();
}
