using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace SmartLine.Iot.Simulacao;

/// <summary>
/// A interface HTTP de um WISE simulado, só o bastante para o SmartLine zerar
/// os contadores: <c>GET</c> e <c>PUT /di_value/slot_0/ch_N</c> com usuário e
/// senha (Basic), no mesmo formato de registro do WISE-4000. Um <c>PUT</c> com
/// <c>"Val": 0</c> num canal de contador reinicia os contadores da máquina.
/// </summary>
/// <remarks>
/// Servidor HTTP mínimo sobre TCP (uma requisição por conexão), para não
/// depender do http.sys do Windows, que exige permissão de administrador para
/// escutar em IPs específicos.
/// </remarks>
public sealed class ServidorHttpWise : IDisposable
{
    private readonly MaquinaSimulada _maquina;
    private readonly SemaphoreSlim _trava;
    private readonly string _autorizacao;
    private readonly TcpListener _ouvinte;
    private readonly CancellationTokenSource _cancelamento = new();

    /// <param name="trava">A mesma trava com que o simulador mexe na máquina.</param>
    /// <param name="porta">0 = qualquer porta livre (ver <see cref="Porta"/>).</param>
    public ServidorHttpWise(MaquinaSimulada maquina, SemaphoreSlim trava, IPAddress endereco, int porta,
        string usuario = "root", string senha = "00000000")
    {
        _maquina = maquina;
        _trava = trava;
        _autorizacao = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{senha}"));
        _ouvinte = new TcpListener(endereco, porta);
        _ouvinte.Start();
        _ = AceitarAsync();
    }

    public int Porta => ((IPEndPoint)_ouvinte.LocalEndpoint).Port;

    public MaquinaSimulada Maquina => _maquina;

    /// <summary>Quantas vezes os contadores foram zerados por HTTP.</summary>
    public int Zeradas { get; private set; }

    private async Task AceitarAsync()
    {
        try
        {
            while (!_cancelamento.IsCancellationRequested)
            {
                var cliente = await _ouvinte.AcceptTcpClientAsync(_cancelamento.Token);
                _ = AtenderAsync(cliente);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task AtenderAsync(TcpClient cliente)
    {
        using (cliente)
        {
            try
            {
                cliente.ReceiveTimeout = 5000;
                var fluxo = cliente.GetStream();
                var (metodo, caminho, cabecalhos, corpo) = await LerRequisicaoAsync(fluxo);
                if (metodo is null)
                    return;

                var resposta = await ResponderAsync(metodo, caminho ?? "/", cabecalhos, corpo);
                var bytes = Encoding.UTF8.GetBytes(resposta);
                await fluxo.WriteAsync(bytes, _cancelamento.Token);
            }
            catch (Exception) when (!_cancelamento.IsCancellationRequested)
            {
                // Conexão abortada ou requisição malformada: só esta chamada se perde.
            }
        }
    }

    private static async Task<(string? Metodo, string? Caminho, Dictionary<string, string> Cabecalhos, string Corpo)> LerRequisicaoAsync(NetworkStream fluxo)
    {
        var buffer = new byte[8192];
        var recebido = new List<byte>();
        int fimCabecalhos;
        while ((fimCabecalhos = Procurar(recebido, "\r\n\r\n"u8.ToArray())) < 0)
        {
            var n = await fluxo.ReadAsync(buffer);
            if (n == 0) return (null, null, [], "");
            recebido.AddRange(buffer.AsSpan(0, n).ToArray());
            if (recebido.Count > 64 * 1024) return (null, null, [], "");
        }

        var textoCabecalhos = Encoding.ASCII.GetString(recebido.ToArray(), 0, fimCabecalhos);
        var linhas = textoCabecalhos.Split("\r\n");
        var primeira = linhas[0].Split(' ');
        if (primeira.Length < 2) return (null, null, [], "");

        var cabecalhos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in linhas.Skip(1))
        {
            var separador = linha.IndexOf(':');
            if (separador > 0)
                cabecalhos[linha[..separador].Trim()] = linha[(separador + 1)..].Trim();
        }

        var tamanho = cabecalhos.TryGetValue("Content-Length", out var t) && int.TryParse(t, out var c) ? c : 0;
        var corpoBytes = recebido.Skip(fimCabecalhos + 4).ToList();
        while (corpoBytes.Count < tamanho)
        {
            var n = await fluxo.ReadAsync(buffer);
            if (n == 0) break;
            corpoBytes.AddRange(buffer.AsSpan(0, n).ToArray());
        }

        return (primeira[0].ToUpperInvariant(), primeira[1], cabecalhos, Encoding.UTF8.GetString(corpoBytes.ToArray()));
    }

    private static int Procurar(List<byte> dados, byte[] marca)
    {
        for (var i = 0; i + marca.Length <= dados.Count; i++)
        {
            var bate = true;
            for (var j = 0; j < marca.Length && bate; j++)
                bate = dados[i + j] == marca[j];
            if (bate) return i;
        }
        return -1;
    }

    private async Task<string> ResponderAsync(string metodo, string caminho, Dictionary<string, string> cabecalhos, string corpo)
    {
        if (!cabecalhos.TryGetValue("Authorization", out var autorizacao) || autorizacao != _autorizacao)
            return Resposta(401, "Unauthorized", "{\"error\":\"unauthorized\"}", "WWW-Authenticate: Basic realm=\"WISE\"\r\n");

        // /di_value/slot_0 (todos) ou /di_value/slot_0/ch_N
        if (!caminho.StartsWith("/di_value/slot_0", StringComparison.OrdinalIgnoreCase))
            return Resposta(404, "Not Found", "{\"error\":\"not found\"}");

        var resto = caminho["/di_value/slot_0".Length..].Trim('/');
        if (resto.Length == 0)
        {
            if (metodo != "GET") return Resposta(405, "Method Not Allowed", "{}");
            var todos = new JsonArray();
            for (var ch = 0; ch < 8; ch++) todos.Add(Registro(ch));
            return Resposta(200, "OK", new JsonObject { ["DIVal"] = todos }.ToJsonString());
        }

        if (!resto.StartsWith("ch_", StringComparison.OrdinalIgnoreCase) || !int.TryParse(resto[3..], out var canal) || canal is < 0 or > 7)
            return Resposta(404, "Not Found", "{\"error\":\"not found\"}");

        if (metodo == "GET")
            return Resposta(200, "OK", Registro(canal).ToJsonString());

        if (metodo != "PUT")
            return Resposta(405, "Method Not Allowed", "{}");

        JsonObject? pedido;
        try { pedido = JsonNode.Parse(corpo) as JsonObject; }
        catch { pedido = null; }
        if (pedido is null)
            return Resposta(400, "Bad Request", "{\"error\":\"bad json\"}");

        // Zerar um contador: o simulador guarda um único tempo rodando, então S2 e S3 zeram juntos.
        if (EhContador(canal) && pedido["Val"] is { } val && val.GetValueKind() == System.Text.Json.JsonValueKind.Number && val.GetValue<double>() == 0)
        {
            await _trava.WaitAsync(_cancelamento.Token);
            try
            {
                _maquina.Reiniciar(DateTime.UtcNow);
                Zeradas++;
            }
            finally { _trava.Release(); }
        }

        return Resposta(200, "OK", Registro(canal).ToJsonString());
    }

    // S2 (ch_1) e S3 (ch_2) são contadores no mapa fixo do SmartLine; S5 e S6 também, mas o
    // simulador não os usa.
    private static bool EhContador(int canal) => canal is 1 or 2 or 4 or 5;

    private JsonObject Registro(int canal)
    {
        var contador = EhContador(canal);
        uint valor = canal switch { 1 => _maquina.ContadorProducao, 2 => _maquina.ContadorRejeito, _ => 0 };
        return new JsonObject
        {
            ["Ch"] = canal,
            ["Md"] = contador ? 1 : 0,   // 0 = entrada digital, 1 = contador
            ["Val"] = contador ? valor : 0,
            ["Stat"] = contador ? 1 : 0, // contador ligado
            ["Uf"] = 0,
            ["Cnt"] = 0,
            ["Ov"] = 0,
            ["Et"] = 0,
        };
    }

    private static string Resposta(int codigo, string texto, string corpo, string cabecalhosExtras = "")
    {
        var bytes = Encoding.UTF8.GetByteCount(corpo);
        return $"HTTP/1.1 {codigo} {texto}\r\nContent-Type: application/json\r\nContent-Length: {bytes}\r\nConnection: close\r\n{cabecalhosExtras}\r\n{corpo}";
    }

    public void Dispose()
    {
        _cancelamento.Cancel();
        _ouvinte.Stop();
        _cancelamento.Dispose();
    }
}
