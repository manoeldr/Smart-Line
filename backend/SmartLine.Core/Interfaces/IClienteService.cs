namespace SmartLine.Core.Interfaces;

public interface IClienteService
{
    Task<IList<ClienteDto>> GetAllAsync();
    Task<ClienteDto?> GetByIdAsync(Guid id);
    Task<IList<LinhaOverviewDto>> GetLinhasAsync(Guid clienteId);
}

public record ClienteDto(
    string Id,
    string Nome,
    string? Estado,
    bool Ativo
);

public record LinhaOverviewDto(
    string Id,
    string ClienteId,
    string Nome,
    bool Ativo,
    IList<MaquinaLinhaOverviewDto> Maquinas
);

public record MaquinaLinhaOverviewDto(
    string Id,
    string LinhaId,
    string MaquinaId,
    string MaquinaNome,
    string TipoColeta,
    decimal VelocidadeNominal,
    bool Critica,
    int Ordem,
    bool Ativo,
    string Status,
    double? Oee,
    bool SessaoAtiva
);
