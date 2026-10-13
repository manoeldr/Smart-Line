using System.Text.Json;
using PDFtoImage;
using SmartLine.Core.Interfaces;

namespace SmartLine.Infrastructure.Repositories;

/// <summary>
/// Guarda a folha timbrada em arquivos ao lado do banco (sem tabela): a imagem (folha.png ou
/// folha.jpg) e um modelo.json com margens, nome e data. O PDF é convertido na importação com
/// o PDFtoImage (motor PDFium embutido; não depende de Word, Acrobat ou LibreOffice no computador).
/// </summary>
public class ModeloRelatorioService : IModeloRelatorioService
{
    // 200 dpi numa A4: ~1654 × 2339 px — nítido na impressão e leve o bastante para a tela
    private const int DpiConversao = 200;
    private const int TamanhoMaximoBytes = 30 * 1024 * 1024;

    public static string Pasta => Path.Combine(AppContext.BaseDirectory, "ModeloRelatorio");
    public static string CaminhoJson => Path.Combine(Pasta, "modelo.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private record Dados(string? NomeArquivo, DateTime? ImportadoEm, long Versao, string Imagem, MargensRelatorioDto Margens);

    public ModeloRelatorioDto Obter()
    {
        var dados = Ler();
        if (dados is null || !File.Exists(Path.Combine(Pasta, dados.Imagem)))
            return new ModeloRelatorioDto(false, null, null, 0, MargensRelatorioDto.Padrao);
        return new ModeloRelatorioDto(true, dados.NomeArquivo, dados.ImportadoEm, dados.Versao, dados.Margens);
    }

    public async Task<ModeloRelatorioDto> ImportarAsync(byte[] arquivo, string nomeArquivo)
    {
        if (arquivo.Length == 0)
            throw new ModeloRelatorioInvalidoException("O arquivo está vazio.");
        if (arquivo.Length > TamanhoMaximoBytes)
            throw new ModeloRelatorioInvalidoException("O arquivo é grande demais (máximo 30 MB).");

        Directory.CreateDirectory(Pasta);
        var anterior = Ler();
        var margens = anterior?.Margens ?? MargensRelatorioDto.Padrao;

        string imagem;
        if (EhPdf(arquivo))
        {
            imagem = "folha.png";
            await Task.Run(() => ConverterPdf(arquivo, Path.Combine(Pasta, imagem)));
        }
        else if (EhPng(arquivo))
        {
            imagem = "folha.png";
            await File.WriteAllBytesAsync(Path.Combine(Pasta, imagem), arquivo);
        }
        else if (EhJpeg(arquivo))
        {
            imagem = "folha.jpg";
            await File.WriteAllBytesAsync(Path.Combine(Pasta, imagem), arquivo);
        }
        else
        {
            throw new ModeloRelatorioInvalidoException(
                "Formato não reconhecido. Importe a folha em PDF (no Word: Arquivo → Salvar como → PDF) ou como imagem PNG/JPG.");
        }

        // Tira a imagem do outro formato, se a anterior era diferente
        foreach (var outra in new[] { "folha.png", "folha.jpg" }.Where(o => o != imagem))
        {
            var caminho = Path.Combine(Pasta, outra);
            if (File.Exists(caminho)) File.Delete(caminho);
        }

        var dados = new Dados(nomeArquivo, DateTime.UtcNow, DateTime.UtcNow.Ticks, imagem, margens);
        Gravar(dados);
        return Obter();
    }

    public ModeloRelatorioDto AtualizarMargens(MargensRelatorioDto margens)
    {
        foreach (var valor in new[] { margens.Superior, margens.Inferior, margens.Esquerda, margens.Direita })
        {
            if (double.IsNaN(valor) || valor < 0 || valor > 100)
                throw new ModeloRelatorioInvalidoException("As margens devem ficar entre 0 e 100 mm.");
        }
        if (margens.Superior + margens.Inferior > 250 || margens.Esquerda + margens.Direita > 180)
            throw new ModeloRelatorioInvalidoException("As margens não deixam espaço para o conteúdo.");

        Directory.CreateDirectory(Pasta);
        var dados = Ler() ?? new Dados(null, null, 0, "folha.png", MargensRelatorioDto.Padrao);
        Gravar(dados with { Margens = margens });
        return Obter();
    }

    public void Remover()
    {
        if (Directory.Exists(Pasta)) Directory.Delete(Pasta, recursive: true);
    }

    public (byte[] Bytes, string ContentType)? LerImagem()
    {
        var dados = Ler();
        if (dados is null) return null;
        var caminho = Path.Combine(Pasta, dados.Imagem);
        if (!File.Exists(caminho)) return null;
        var tipo = dados.Imagem.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";
        return (File.ReadAllBytes(caminho), tipo);
    }

    // ── arquivos ──────────────────────────────────────────────

    private static Dados? Ler()
    {
        try
        {
            if (!File.Exists(CaminhoJson)) return null;
            return JsonSerializer.Deserialize<Dados>(File.ReadAllText(CaminhoJson), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void Gravar(Dados dados) =>
        File.WriteAllText(CaminhoJson, JsonSerializer.Serialize(dados, JsonOptions));

    private static void ConverterPdf(byte[] pdf, string destinoPng)
    {
        try
        {
            using var origem = new MemoryStream(pdf);
            // Só a primeira página: a folha timbrada é uma só para todas as páginas do relatório
            Conversion.SavePng(destinoPng, origem, page: 0, options: new RenderOptions(Dpi: DpiConversao));
        }
        catch (Exception e)
        {
            throw new ModeloRelatorioInvalidoException($"Não foi possível ler o PDF: {e.Message}");
        }
    }

    private static bool EhPdf(byte[] b) => b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46; // %PDF
    private static bool EhPng(byte[] b) => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
    private static bool EhJpeg(byte[] b) => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
}
