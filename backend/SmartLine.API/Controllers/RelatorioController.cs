using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.API.Controllers;

/// <summary>
/// Relatório para o cliente em PDF, gerado no servidor: o navegador só baixa o arquivo, sem a
/// caixa de impressão. (O envio por e-mail é feito pelo desktop, no programa de e-mail do PC.)
/// </summary>
[ApiController]
[Route("api/relatorios")]
[Authorize]
public class RelatorioController : ControllerBase
{
    private readonly IRelatorioPdfService _pdf;
    private readonly SmartLineDbContext _context;

    public RelatorioController(IRelatorioPdfService pdf, SmartLineDbContext context)
    {
        _pdf = pdf;
        _context = context;
    }

    public record PdfRequest(string LinhaId, string Data, string Produto, string ResponsavelCliente);

    [HttpPost("pdf")]
    public async Task<IActionResult> Pdf([FromBody] PdfRequest req)
    {
        try
        {
            var (bytes, nome) = await GerarAsync(req.LinhaId, req.Data, req.Produto, req.ResponsavelCliente);
            return File(bytes, "application/pdf", nome);
        }
        catch (RelatorioPdfException e) { return BadRequest(new { mensagem = e.Message }); }
    }

    // Gera o PDF abrindo a tela como o usuário logado; o nome segue o padrão
    // SmartLineScan_[Cliente]_[Linha]_[dd-mm-aaaa].pdf
    private async Task<(byte[] Bytes, string Nome)> GerarAsync(string linhaId, string data, string produto, string responsavel)
    {
        if (!Guid.TryParse(linhaId, out var lid)) throw new RelatorioPdfException("Linha inválida.");
        var linha = await _context.Linhas.Include(l => l.Cliente).FirstOrDefaultAsync(l => l.Id == lid)
            ?? throw new RelatorioPdfException("Linha não encontrada.");

        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        var clienteId = User.FindFirst("clienteId")?.Value;
        if (string.IsNullOrEmpty(clienteId)) clienteId = linha.ClienteId.ToString();

        // De onde o navegador sem janela abre a tela: o próprio servidor (o frontend buildado em
        // wwwroot) ou, em desenvolvimento, o Vite (RELATORIO_URL_BASE=http://localhost:5173 no .env)
        var urlBase = Environment.GetEnvironmentVariable("RELATORIO_URL_BASE");
        if (string.IsNullOrWhiteSpace(urlBase)) urlBase = $"{Request.Scheme}://{Request.Host}";

        var bytes = await _pdf.GerarAsync(urlBase, token, clienteId, new ParametrosRelatorio(linhaId, data, produto, responsavel));

        var partes = data.Split('-');
        var dataNome = partes.Length == 3 ? $"{partes[2]}-{partes[1]}-{partes[0]}" : data;
        var nome = $"SmartLineScan_{Limpar(linha.Cliente?.Nome ?? "Cliente")}_{Limpar(linha.Nome)}_{dataNome}.pdf";
        return (bytes, nome);
    }

    private static string Limpar(string texto)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        return string.Concat(texto.Trim().Select(c => invalidos.Contains(c) ? '-' : c));
    }
}
