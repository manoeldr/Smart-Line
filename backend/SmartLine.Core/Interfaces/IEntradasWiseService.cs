using SmartLine.Core.Iot;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Textos das entradas do WISE por máquina do catálogo: como cada entrada
/// aparece nas regras, no Validar entradas e no detalhe da máquina.
/// </summary>
public interface IEntradasWiseService
{
    /// <summary>As 8 entradas com o texto em uso (personalizado ou padrão); nulo se a máquina não existe.</summary>
    Task<IReadOnlyList<EntradaWiseDto>?> ObterAsync(Guid maquinaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Personaliza as entradas enviadas (as outras ficam como estão). Uma
    /// entrada salva igual ao padrão deixa de ser personalizada. Devolve as 8.
    /// </summary>
    Task<ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>> SalvarAsync(
        Guid maquinaId, IList<SalvarEntradaWiseRequest> entradas, CancellationToken cancellationToken = default);

    /// <summary>Volta todas as entradas da máquina ao texto padrão; nulo se a máquina não existe.</summary>
    Task<IReadOnlyList<EntradaWiseDto>?> RestaurarPadraoAsync(Guid maquinaId, CancellationToken cancellationToken = default);
}

/// <param name="Entrada">Número da entrada física no WISE (0 a 7; S1 = DI0).</param>
/// <param name="TextoAtivo">Sensor em alarme (ex.: "Falta de garrafas na entrada"). Nulo nos contadores.</param>
/// <param name="TextoNormal">Sensor normal (ex.: "Com garrafas na entrada"). Nulo nos contadores.</param>
/// <param name="Personalizado">Algum texto foi trocado nesta máquina.</param>
public record EntradaWiseDto(
    CanalWise Canal,
    int Entrada,
    TipoCanal Tipo,
    FuncaoCanal Funcao,
    string Nome,
    string? TextoAtivo,
    string? TextoNormal,
    bool Personalizado);

/// <param name="TextoAtivo">Obrigatório nos sensores; ignorado nos contadores.</param>
/// <param name="TextoNormal">Obrigatório nos sensores; ignorado nos contadores.</param>
public record SalvarEntradaWiseRequest(CanalWise Canal, string Nome, string? TextoAtivo = null, string? TextoNormal = null);
