using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace SmartLine.Desktop;

public partial class Form1 : Form
{
    private readonly WebView2 _webView = new();
    private Process? _processoBackend;
    private const string UrlBackend = "http://localhost:5278";

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
            IniciarBackend();
            await AguardarBackendSubirAsync();

            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Navigate(UrlBackend);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao iniciar: {ex.Message}\n\n{ex.StackTrace}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Sobe o backend (SmartLine.API.exe) como processo filho, oculto, na mesma pasta do app.
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
        // o Kestrel usa 5000 por padrão, mas o resto do app (aqui) assume 5278.
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = UrlBackend;

        _processoBackend = new Process { StartInfo = startInfo };
        _processoBackend.Start();
    }

    // Espera o backend responder antes de navegar — evita mostrar erro de conexão recusada.
    private async Task AguardarBackendSubirAsync()
    {
        using var http = new HttpClient();
        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            try
            {
                var resposta = await http.GetAsync($"{UrlBackend}/api/licenca/status");
                if (resposta.IsSuccessStatusCode || (int)resposta.StatusCode == 403) return;
            }
            catch
            {
                // backend ainda não subiu — tenta de novo
            }
            await Task.Delay(500);
        }
    }

    // Encerra o backend junto quando a janela fecha — evita processo fantasma rodando.
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