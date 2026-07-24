namespace SmartLine.Core.Interfaces;

public interface ILinhaMaquinaService
{
    Task<IList<MaquinaLinhaConfDto>> GetMaquinasDaLinhaAsync(Guid linhaId);
    Task<MaquinaLinhaConfDto> AdicionarMaquinaAsync(Guid linhaId, Guid maquinaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade);
    Task<MaquinaLinhaConfDto?> AtualizarAsync(Guid maquinaLinhaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade);
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
    bool Ativo
);

public record ReordenarItem(Guid MaquinaLinhaId, int Ordem);
