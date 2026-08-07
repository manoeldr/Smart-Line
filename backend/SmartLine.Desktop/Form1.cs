using System.Diagnostics;
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

        Text = "SmartLine";
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
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://localhost:5278";

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