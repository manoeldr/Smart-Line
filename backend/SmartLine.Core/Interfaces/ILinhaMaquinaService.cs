namespace SmartLine.Core.Interfaces;

public interface ILinhaMaquinaService
{
    Task<IList<MaquinaLinhaConfDto>> GetMaquinasDaLinhaAsync(Guid linhaId);
    /// <param name="tempoDeteccaoParadaSegundos">Z do Semi Automático; nulo = 60 s.</param>
    Task<MaquinaLinhaConfDto> AdicionarMaquinaAsync(Guid linhaId, Guid maquinaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade, bool medeProducao, int? tempoDeteccaoParadaSegundos = null);

    /// <param name="tempoDeteccaoParadaSegundos">Z do Semi Automático; nulo = mantém o atual.</param>
    Task<MaquinaLinhaConfDto?> AtualizarAsync(Guid maquinaLinhaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade, bool medeProducao, int? tempoDeteccaoParadaSegundos = null);
    Task<bool> RemoverMaquinaAsync(Guid maquinaLinhaId);
    Task ReordenarAsync(Guid linhaId, IList<ReordenarItem> ordens);
}

public record MaquinaLinhaConfDto(
    string Id,
    string LinhaId,
    string MaquinaId,
    string MaquinaNome,
    int Ordem,
    bool Critica,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    bool MedeProducao,
    bool Ativo,
    int TempoDeteccaoParadaSegundos,
    bool RegrasPersonalizadas
);

/// <summary>Limites do tempo para detectar parada (Z) aceitos no cadastro.</summary>
public static class LimitesTempoDeteccaoParada
{
    /// <summary>Menos que isso confunde o intervalo de publicação do WISE com parada.</summary>
    public const int MinimoSegundos = 10;
    public const int MaximoSegundos = 3600;
}

public record ReordenarItem(Guid MaquinaLinhaId, int Ordem);