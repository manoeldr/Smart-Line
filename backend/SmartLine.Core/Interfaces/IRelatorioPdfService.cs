namespace SmartLine.Core.Interfaces;

/// <summary>
/// Gera o PDF do relatório para o cliente no servidor: abre a própria tela de Relatórios num
/// navegador sem janela (o Edge/Chrome já instalado no computador) e imprime em PDF — o mesmo
/// resultado do "Salvar como PDF", sem a caixa de impressão e em qualquer máquina.
/// </summary>
public interface IRelatorioPdfService
{
    /// <param name="urlBase">Endereço onde a tela está servida (ex.: http://localhost:5278).</param>
    /// <param name="token">Token de login de quem pediu: a tela abre como esse usuário.</param>
    /// <param name="clienteId">Cliente escolhido na tela de quem pediu.</param>
    Task<byte[]> GerarAsync(string urlBase, string token, string clienteId, ParametrosRelatorio parametros);
}

/// <param name="Blocos">Blocos ligados nas páginas das máquinas, separados por vírgula (a tela sabe os nomes).</param>
public record ParametrosRelatorio(string LinhaId, string Data, string Produto, string ResponsavelCliente, string Blocos = "");

public class RelatorioPdfException : Exception
{
    public RelatorioPdfException(string mensagem, Exception? causa = null) : base(mensagem, causa) { }
}
