using System.Web;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SmartLine.Core.Interfaces;

namespace SmartLine.Infrastructure.Relatorios;

/// <summary>
/// PuppeteerSharp apontado para um navegador já instalado (Edge vem com o Windows), sem baixar
/// o Chromium. Caminho forçado por NAVEGADOR_PDF no .env, se precisar. A tela de Relatórios,
/// aberta com ?impressao=1, marca data-relatorio-pronto="1" quando terminou de desenhar.
/// </summary>
public class RelatorioPdfService : IRelatorioPdfService
{
    private static readonly string[] CaminhosWindows =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    ];
    private static readonly string[] CaminhosLinux =
    [
        "/usr/bin/chromium", "/usr/bin/chromium-browser", "/usr/bin/google-chrome", "/usr/bin/microsoft-edge",
    ];

    // Um PDF por vez: cada geração abre um navegador inteiro; dois ao mesmo tempo só disputam CPU.
    private static readonly SemaphoreSlim Fila = new(1, 1);

    public static string? EncontrarNavegador()
    {
        var forcado = Environment.GetEnvironmentVariable("NAVEGADOR_PDF");
        if (!string.IsNullOrWhiteSpace(forcado) && File.Exists(forcado)) return forcado;
        var candidatos = OperatingSystem.IsWindows() ? CaminhosWindows : CaminhosLinux;
        return candidatos.FirstOrDefault(File.Exists);
    }

    public async Task<byte[]> GerarAsync(string urlBase, string token, string clienteId, ParametrosRelatorio p)
    {
        var navegador = EncontrarNavegador()
            ?? throw new RelatorioPdfException("Nenhum navegador encontrado neste computador para gerar o PDF (Microsoft Edge ou Google Chrome).");

        var consulta = HttpUtility.ParseQueryString(string.Empty);
        consulta["linha"] = p.LinhaId;
        consulta["data"] = p.Data;
        consulta["produto"] = p.Produto;
        consulta["responsavel"] = p.ResponsavelCliente;
        consulta["impressao"] = "1";
        var url = $"{urlBase.TrimEnd('/')}/relatorios?{consulta}";

        await Fila.WaitAsync();
        try
        {
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = navegador,
                Args = ["--no-sandbox", "--disable-gpu", "--no-first-run", "--no-default-browser-check"],
            });
            await using var page = await browser.NewPageAsync();
            await page.SetViewportAsync(new ViewPortOptions { Width = 1000, Height = 1400 });
            // A tela lê o login do localStorage: entra como quem pediu, no cliente escolhido
            await page.EvaluateFunctionOnNewDocumentAsync(
                "(t, c) => { localStorage.setItem('token', t); localStorage.setItem('clienteId', c); }", token, clienteId);
            await page.GoToAsync(url, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0], Timeout = 60_000 });
            await page.WaitForSelectorAsync("[data-relatorio-pronto='1']", new WaitForSelectorOptions { Timeout = 60_000 });
            // Imagens (a folha timbrada) carregadas antes de imprimir
            await page.WaitForFunctionAsync("() => Array.from(document.images).every(i => i.complete)", new WaitForFunctionOptions { Timeout = 30_000 });

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
                MarginOptions = new MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" },
            });
        }
        catch (RelatorioPdfException) { throw; }
        catch (Exception e)
        {
            throw new RelatorioPdfException($"Não foi possível gerar o PDF: {e.Message}", e);
        }
        finally
        {
            Fila.Release();
        }
    }
}
