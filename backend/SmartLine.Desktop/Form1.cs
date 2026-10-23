using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SmartLine.Desktop;

public partial class Form1 : Form
{
    private readonly WebView2 _webView = new();
    private Process? _processoBackend;

    // Arquivo de configuração simples ao lado do .exe. Vazio (ou inexistente) = modo local
    // (sobe o próprio backend, como sempre funcionou). Com um IP preenchido = modo cliente
    // (não sobe backend nenhum, só conecta no servidor indicado pela rede).
    private const string NomeArquivoServidor = "servidor.txt";
    private const int PortaPadrao = 5278;
    private string _urlBackend = "http://localhost:5278";
    private bool _modoCliente;
    // O que está escrito no servidor.txt agora (vazio = este PC é o servidor)
    private string _enderecoServidor = "";

    public Form1()
    {
        InitializeComponent();

        Text = "Smart Line Scan";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        try
        {
            LerConfiguracaoServidor();

            if (!_modoCliente)
            {
                IniciarBackend();
            }

            await AguardarBackendSubirAsync();

            await _webView.EnsureCoreWebView2Async();
            // Pedidos da página (ex.: exportar o relatório em PDF sem a caixa de impressão)
            _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            // A tela Configurações → Servidor precisa saber como ESTE computador está configurado.
            // Pela API não dava: em modo cliente a tela vem do servidor, e uma chamada de API
            // configuraria o servidor, não esta estação. O aplicativo é sempre o PC local.
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"window.smartlineDesktop = {SituacaoDesteComputador()};");
            _webView.CoreWebView2.Navigate(_urlBackend);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao iniciar: {ex.Message}\n\n{ex.StackTrace}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Lê o servidor.txt ao lado do .exe. Se não existir, cria vazio (fica fácil de achar e editar
    // depois, sem precisar saber o nome do arquivo de cabeça). Uma linha só, com IP ou IP:porta.
    private void LerConfiguracaoServidor()
    {
        var caminhoArquivo = Path.Combine(AppContext.BaseDirectory, NomeArquivoServidor);

        if (!File.Exists(caminhoArquivo))
        {
            File.WriteAllText(caminhoArquivo, "");
        }

        var conteudo = File.ReadAllText(caminhoArquivo).Trim();

        if (string.IsNullOrEmpty(conteudo))
        {
            _modoCliente = false;
            _urlBackend = "http://localhost:5278";
            return;
        }

        _modoCliente = true;
        _enderecoServidor = conteudo;
        // Aceita tanto "192.168.10.1" quanto "192.168.10.1:5278" já com porta customizada
        _urlBackend = conteudo.Contains(':') ? $"http://{conteudo}" : $"http://{conteudo}:{PortaPadrao}";
    }

    // Sobe o backend (SmartLine.API.exe) como processo filho, oculto, na mesma pasta do app.
    // Só é chamado em modo local — no modo cliente, o backend já está rodando em outra máquina.
    private void IniciarBackend()
    {
        var caminhoBackend = Path.Combine(AppContext.BaseDirectory, "SmartLine.API.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = caminhoBackend,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        // Força a porta explicitamente — em produção (fora do dotnet run/launchSettings.json),
        // o Kestrel usa 5000 por padrão, mas o resto do app assume 5278.
        // Escuta em todas as interfaces (0.0.0.0), não só em localhost: este PC é o servidor das
        // estações, que apontam para o IP dele no servidor.txt delas. Quem não usa estações não
        // é afetado — sem a regra de firewall da porta 5278, ninguém de fora alcança.
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://0.0.0.0:5278";

        _processoBackend = new Process { StartInfo = startInfo };
        _processoBackend.Start();
    }

    // Espera o backend (local ou remoto) responder antes de navegar — evita mostrar erro de
    // conexão recusada. Em modo cliente, também serve pra avisar se o servidor está fora do ar.
    private async Task AguardarBackendSubirAsync()
    {
        // Timeout curto por tentativa — sem isso, o HttpClient pode esperar até 100s por tentativa
        // quando o IP está inalcançável, fazendo o app parecer travado por minutos em vez de segundos.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var tentativa = 0; tentativa < 15; tentativa++)
        {
            try
            {
                var resposta = await http.GetAsync($"{_urlBackend}/api/licenca/status");
                if (resposta.IsSuccessStatusCode || (int)resposta.StatusCode == 403) return;
            }
            catch
            {
                // backend ainda não respondeu — tenta de novo
            }
            await Task.Delay(500);
        }

        if (_modoCliente)
        {
            MessageBox.Show(
                $"Não foi possível conectar ao servidor em {_urlBackend}.\n\nVerifique se o PC central está ligado, com o SmartLine aberto, e se ambos estão na mesma rede.",
                "Servidor não encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Mensagens da página (window.chrome.webview.postMessage):
    //   { "tipo": "exportarPdf", "nome": "..." } — grava o PDF direto, sem a caixa de impressão do
    //     navegador: só o "Salvar como" do Windows, já com o nome padrão do arquivo;
    //   { "tipo": "enviarEmail", "nome", "para", "assunto", "mensagem" } — grava o PDF numa pasta
    //     temporária e abre a mensagem no programa de e-mail com ele anexado (ver EmailMapi);
    //   { "tipo": "testarServidor", "endereco" } — vê se o servidor responde (sem CORS, é o app
    //     que consulta, não a página);
    //   { "tipo": "salvarServidor", "modo": "servidor"|"cliente", "endereco" } — grava o
    //     servidor.txt DESTE computador e reinicia o aplicativo;
    //   { "tipo": "liberarFirewall" } — cria as regras de entrada do Windows (pede elevação).
    private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            var tipo = doc.RootElement.TryGetProperty("tipo", out var t) ? t.GetString() : null;

            string Campo(string campo) => doc.RootElement.TryGetProperty(campo, out var v) ? v.GetString() ?? "" : "";

            switch (tipo)
            {
                case "testarServidor":
                    await TestarServidorAsync(Campo("endereco"));
                    return;
                case "salvarServidor":
                    SalvarServidor(Campo("modo"), Campo("endereco"));
                    return;
                case "liberarFirewall":
                    await LiberarFirewallAsync();
                    return;
            }

            if (tipo != "exportarPdf" && tipo != "enviarEmail") return;

            var nome = doc.RootElement.TryGetProperty("nome", out var n) ? n.GetString() : null;
            var nomeArquivo = string.IsNullOrWhiteSpace(nome) ? "SmartLineScan_Relatorio" : nome!;

            if (tipo == "exportarPdf")
            {
                await ExportarPdfAsync(nomeArquivo);
            }
            else if (tipo == "enviarEmail")
            {
                await EnviarEmailAsync(nomeArquivo, Campo("para"), Campo("assunto"), Campo("mensagem"));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível atender o pedido da tela: {ex.Message}", "Smart Line Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ExportarPdfAsync(string nomeSugerido)
    {
        using var dialogo = new SaveFileDialog
        {
            Title = "Salvar relatório em PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = nomeSugerido + ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;

        var opcoes = OpcoesPdf();
        var ok = await _webView.CoreWebView2.PrintToPdfAsync(dialogo.FileName, opcoes);
        if (!ok)
        {
            MessageBox.Show("Não foi possível gravar o PDF.", "Exportar PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Avisa a página, que mostra "PDF salvo" e o caminho
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "pdfExportado", caminho = dialogo.FileName }));
    }

    private async Task EnviarEmailAsync(string nomeArquivo, string para, string assunto, string mensagem)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "SmartLineScan", "relatorios");
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, nomeArquivo + ".pdf");

        var opcoes = OpcoesPdf();
        var ok = await _webView.CoreWebView2.PrintToPdfAsync(caminho, opcoes);
        if (!ok)
        {
            MessageBox.Show("Não foi possível gerar o PDF.", "Enviar por e-mail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // O MAPI abre a janela do programa de e-mail e só volta quando ela fecha: fora da thread da
        // interface, para o SmartLine não travar enquanto a pessoa escreve — mas numa thread STA,
        // que é o que o Outlook exige (numa thread comum a chamada falha sem aviso).
        var janela = Handle;
        var erro = await EmailMapi.AbrirMensagemEmThreadStaAsync(janela, para, assunto, mensagem, caminho);
        if (erro is null)
        {
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "emailAberto", caminho }));
            return;
        }

        // Sem Outlook clássico (ex.: só o "novo Outlook", que não aceita anexo de outro programa):
        // abre a mensagem por mailto: (destinatário, assunto e texto) e a pasta com o PDF
        // selecionado, para arrastar o anexo para a mensagem.
        try
        {
            var mailto = $"mailto:{Uri.EscapeDataString(para)}?subject={Uri.EscapeDataString(assunto)}&body={Uri.EscapeDataString(mensagem)}";
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            Process.Start("explorer.exe", $"/select,\"{caminho}\"");
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                tipo = "emailSemAnexo",
                caminho,
                mensagem = "A mensagem abriu sem o anexo: arraste o PDF da pasta que abriu para dentro dela.",
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(erro + "\n\nO PDF foi gravado em:\n" + caminho + "\n\n" + ex.Message, "Enviar por e-mail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo = "emailErro", mensagem = erro, caminho }));
        }
    }

    // Mesma saída do "Salvar como PDF" do navegador: A4 retrato, sem margem (a folha do
    // relatório já tem as suas), fundos e imagens (a folha timbrada) impressos.
    private CoreWebView2PrintSettings OpcoesPdf()
    {
        var opcoes = _webView.CoreWebView2.Environment.CreatePrintSettings();
        opcoes.Orientation = CoreWebView2PrintOrientation.Portrait;
        opcoes.PageWidth = 8.27;   // polegadas (210 mm)
        opcoes.PageHeight = 11.69; // polegadas (297 mm)
        opcoes.MarginTop = 0;
        opcoes.MarginBottom = 0;
        opcoes.MarginLeft = 0;
        opcoes.MarginRight = 0;
        opcoes.ShouldPrintBackgrounds = true;
        opcoes.ShouldPrintHeaderAndFooter = false;
        opcoes.ScaleFactor = 1;
        return opcoes;
    }

    // ── Configuração de servidor/estação (Configurações → Servidor) ─────────────────────
    // Tudo aqui é do computador LOCAL: o servidor.txt fica ao lado do .exe e só o aplicativo
    // consegue gravá-lo na máquina certa (em modo cliente a tela vem do servidor).

    private string SituacaoDesteComputador() => JsonSerializer.Serialize(new
    {
        modoCliente = _modoCliente,
        endereco = _enderecoServidor,
        porta = PortaPadrao,
        ips = IpsDaRede(),
    });

    // Placas que não são rede de verdade: máquinas virtuais, WSL/Hyper-V, VPNs e afins. Mostrar
    // o IP delas só confunde quem vai digitar o endereço na estação — nenhuma estação chega por ali.
    private static readonly string[] PlacasVirtuais =
        ["virtualbox", "vmware", "hyper-v", "vethernet", "wsl", "docker", "tap-", "tap adapter",
         "tunnel", "tailscale", "zerotier", "loopback", "bluetooth", "npcap", "pseudo"];

    /// <summary>
    /// Endereços deste computador que uma estação consegue usar: IPv4 das placas de rede ligadas,
    /// sem as virtuais e sem o endereço que o Windows inventa quando a placa não acha a rede
    /// (169.254.x.x). Primeiro as placas com saída para a rede (com gateway), que é o caso normal.
    /// Se o filtro não sobrar nada, mostra tudo — melhor um endereço a mais que nenhum.
    /// </summary>
    private static string[] IpsDaRede()
    {
        var placas = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        static IEnumerable<string> Enderecos(NetworkInterface n) =>
            n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Where(ip => !ip.StartsWith("169.254."));

        static bool EhVirtual(NetworkInterface n)
        {
            var identificacao = (n.Description + " " + n.Name).ToLowerInvariant();
            return PlacasVirtuais.Any(identificacao.Contains);
        }

        static bool TemSaida(NetworkInterface n) =>
            n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

        var reais = placas.Where(n => !EhVirtual(n)).ToList();
        var ips = reais.Where(TemSaida).SelectMany(Enderecos)
            .Concat(reais.Where(n => !TemSaida(n)).SelectMany(Enderecos))
            .Distinct()
            .ToArray();

        return ips.Length > 0 ? ips : placas.SelectMany(Enderecos).Distinct().ToArray();
    }

    private void Responder(object resposta) =>
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(resposta));

    private static string ComPorta(string endereco) =>
        endereco.Contains(':') ? endereco : $"{endereco}:{PortaPadrao}";

    /// <summary>O servidor indicado responde? Quem pergunta é o aplicativo (a página esbarraria no CORS).</summary>
    private async Task TestarServidorAsync(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            Responder(new { tipo = "servidorTestado", ok = false, mensagem = "Informe o endereço do servidor." });
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var resposta = await http.GetAsync($"http://{ComPorta(endereco.Trim())}/api/licenca/status");
            var ok = resposta.IsSuccessStatusCode || (int)resposta.StatusCode == 403;
            Responder(new
            {
                tipo = "servidorTestado",
                ok,
                mensagem = ok ? "Servidor encontrado." : $"O endereço respondeu, mas não parece ser o Smart Line Scan (código {(int)resposta.StatusCode})."
            });
        }
        catch
        {
            Responder(new
            {
                tipo = "servidorTestado",
                ok = false,
                mensagem = "Não foi possível falar com esse endereço. Veja se o PC servidor está ligado com o Smart Line Scan aberto, na mesma rede, e com a porta liberada no firewall dele."
            });
        }
    }

    /// <summary>Grava o servidor.txt deste computador e reinicia o aplicativo para valer.</summary>
    private void SalvarServidor(string modo, string endereco)
    {
        var comoCliente = modo == "cliente";
        var valor = comoCliente ? endereco.Trim() : "";

        if (comoCliente && string.IsNullOrWhiteSpace(valor))
        {
            Responder(new { tipo = "servidorSalvo", ok = false, mensagem = "Informe o endereço do servidor." });
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, NomeArquivoServidor), valor);
        }
        catch (Exception ex)
        {
            Responder(new { tipo = "servidorSalvo", ok = false, mensagem = $"Não foi possível gravar a configuração: {ex.Message}" });
            return;
        }

        Responder(new { tipo = "servidorSalvo", ok = true, mensagem = "Configuração salva. O Smart Line Scan vai reiniciar." });
        // Deixa a tela mostrar o aviso antes de fechar.
        var reinicio = new System.Windows.Forms.Timer { Interval = 1500 };
        reinicio.Tick += (_, _) => { reinicio.Stop(); Application.Restart(); };
        reinicio.Start();
    }

    /// <summary>
    /// Cria as regras de entrada do Windows para este PC servir as estações e receber os WISE:
    /// 5278 (sistema), 1883 (WISE) e ping. Precisa de elevação — o Windows pede a confirmação
    /// uma vez só, porque tudo vai num arquivo de comandos.
    /// </summary>
    private async Task LiberarFirewallAsync()
    {
        var bat = Path.Combine(Path.GetTempPath(), "smartline-firewall.bat");
        var comandos = new[]
        {
            "@echo off",
            Regra("Smart Line Scan - API", $"protocol=TCP localport={PortaPadrao}"),
            Regra("Smart Line Scan - MQTT", "protocol=TCP localport=1883"),
            Regra("Smart Line Scan - Ping", "protocol=icmpv4:8,any"),
            "exit /b 0",
        };

        try
        {
            File.WriteAllLines(bat, comandos);
            using var processo = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{bat}\"",
                UseShellExecute = true,   // obrigatório para pedir elevação
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            // Esperar sem travar a janela: a pessoa ainda tem que confirmar no Windows.
            using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var terminou = false;
            if (processo is not null)
            {
                try
                {
                    await processo.WaitForExitAsync(prazo.Token);
                    terminou = true;
                }
                catch (OperationCanceledException)
                {
                    // demorou demais — segue como não concluído
                }
            }

            var ok = terminou && processo!.ExitCode == 0;
            Responder(new
            {
                tipo = "firewallResultado",
                ok,
                mensagem = ok
                    ? $"Portas liberadas no firewall deste computador ({PortaPadrao}, 1883 e ping)."
                    : $"O Windows não aceitou criar as regras. Peça para a TI liberar as portas {PortaPadrao} e 1883 de entrada."
            });
        }
        catch (Exception ex)
        {
            // Cancelar a janela do Windows cai aqui (ERROR_CANCELLED)
            Responder(new
            {
                tipo = "firewallResultado",
                ok = false,
                mensagem = $"Não foi possível liberar no firewall: {ex.Message}"
            });
        }
        finally
        {
            try { File.Delete(bat); } catch { /* arquivo temporário */ }
        }
    }

    private static string Regra(string nome, string filtro) =>
        $"netsh advfirewall firewall delete rule name=\"{nome}\" >nul 2>&1" + Environment.NewLine +
        $"netsh advfirewall firewall add rule name=\"{nome}\" dir=in action=allow {filtro} >nul || exit /b 1";

    // Encerra o backend junto quando a janela fecha — evita processo fantasma rodando.
    // Não faz nada em modo cliente, já que não há backend local pra encerrar.
    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            if (_processoBackend is { HasExited: false })
            {
                _processoBackend.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // já pode ter encerrado sozinho
        }
    }
}