namespace SmartLine.Core.Interfaces;

/// <summary>
/// Garante que cada máquina do catálogo tenha as regras padrão do Semi Automático.
/// Só cria quando a máquina ainda não tem conjunto de regras: nunca sobrescreve
/// o que alguém editou.
/// </summary>
public interface IRegrasPadraoService
{
    /// <summary>Cria o padrão para uma máquina, se ela ainda não tiver. Devolve se criou.</summary>
    Task<bool> GarantirAsync(Guid maquinaId, CancellationToken cancellationToken = default);

    /// <summary>Cria o padrão para toda máquina ativa que ainda não tiver. Devolve quantas.</summary>
    Task<int> GarantirParaTodasAsync(CancellationToken cancellationToken = default);
}
