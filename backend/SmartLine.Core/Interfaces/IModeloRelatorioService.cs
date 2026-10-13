namespace SmartLine.Core.Interfaces;

/// <summary>
/// Folha timbrada do relatório para o cliente: uma por instalação (é da Sanmartin, não do
/// cliente), importada em Configurações → Relatório. Fica como imagem A4 ao lado do banco
/// (pasta ModeloRelatorio) e vai no fundo de cada página do relatório, na tela e no PDF.
/// As margens dizem onde o conteúdo pode ser desenhado sem cobrir o timbre.
/// </summary>
public interface IModeloRelatorioService
{
    /// <summary>Situação atual: Existe = false quando nunca foi importada (relatório sai sem timbre).</summary>
    ModeloRelatorioDto Obter();

    /// <summary>
    /// Importa a folha: PDF (primeira página, convertida em imagem) ou imagem PNG/JPG.
    /// Substitui a anterior. Lança <see cref="ModeloRelatorioInvalidoException"/> se o arquivo não servir.
    /// </summary>
    Task<ModeloRelatorioDto> ImportarAsync(byte[] arquivo, string nomeArquivo);

    ModeloRelatorioDto AtualizarMargens(MargensRelatorioDto margens);

    void Remover();

    /// <summary>Bytes da imagem da folha e o tipo (image/png ou image/jpeg); nulo sem folha.</summary>
    (byte[] Bytes, string ContentType)? LerImagem();
}

/// <summary>Margens em milímetros: área livre do timbre em cada borda da página A4.</summary>
public record MargensRelatorioDto(double Superior, double Inferior, double Esquerda, double Direita)
{
    public static MargensRelatorioDto Padrao => new(30, 25, 16, 16);
}

/// <param name="Existe">Há folha importada.</param>
/// <param name="NomeArquivo">Nome do arquivo que foi importado (só para mostrar).</param>
/// <param name="ImportadoEm">Quando foi importada (UTC).</param>
/// <param name="Versao">Muda a cada importação: a tela usa para não reaproveitar a imagem antiga.</param>
public record ModeloRelatorioDto(
    bool Existe,
    string? NomeArquivo,
    DateTime? ImportadoEm,
    long Versao,
    MargensRelatorioDto Margens);

public class ModeloRelatorioInvalidoException : Exception
{
    public ModeloRelatorioInvalidoException(string mensagem) : base(mensagem) { }
}
