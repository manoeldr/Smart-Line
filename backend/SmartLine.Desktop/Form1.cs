using System.Diagnostics;
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
    private string _urlBackend = "http://localhost:5278";
    private bool _modoCliente;

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
        // Aceita tanto "192.168.10.1" quanto "192.168.10.1:5278" já com porta customizada
        _urlBackend = conteudo.Contains(':') ? $"http://{conteudo}" : $"http://{conteudo}:5278";
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
    //     temporária e abre a mensagem no programa de e-mail com ele anexado (ver EmailMapi).
    private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            var tipo = doc.RootElement.TryGetProperty("tipo", out var t) ? t.GetString() : null;
            if (tipo != "exportarPdf" && tipo != "enviarEmail") return;

            var nome = doc.RootElement.TryGetProperty("nome", out var n) ? n.GetString() : null;
            var nomeArquivo = string.IsNullOrWhiteSpace(nome) ? "SmartLineScan_Relatorio" : nome!;

            if (tipo == "exportarPdf")
            {
                await ExportarPdfAsync(nomeArquivo);
            }
            else if (tipo == "enviarEmail")
            {
                string Texto(string campo) => doc.RootElement.TryGetProperty(campo, out var v) ? v.GetString() ?? "" : "";
                await EnviarEmailAsync(nomeArquivo, Texto("para"), Texto("assunto"), Texto("mensagem"));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível exportar o PDF: {ex.Message}", "Exportar PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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