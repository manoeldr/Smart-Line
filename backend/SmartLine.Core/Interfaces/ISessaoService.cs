namespace SmartLine.Core.Interfaces;

public interface ISessaoService
{
    Task<SessaoDto?> AbrirAsync(Guid maquinaLinhaId, Guid usuarioId, AbrirSessaoRequest req);
    Task<bool> FecharAsync(Guid sessaoId);
    Task<SessaoDto?> GetByIdAsync(Guid sessaoId);
    Task<SessaoDto?> EstenderAsync(Guid sessaoId, DateTime novaPrevisaoTermino);
    Task<bool> FinalizarComLeituraAsync(Guid sessaoId, FinalizarSessaoRequest req);
}

public record SessaoDto(
    string Id,
    string MaquinaLinhaId,
    string UsuarioId,
    DateTime Inicio,
    DateTime? Fim,
    DateTime? PrevisaoTermino,
    string Status,
    string TipoColeta,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    IList<string> CamposSelecionados
);

public record AbrirSessaoRequest(
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    DateTime? PrevisaoTermino,
    string TipoColeta,
    IList<Guid> CampoMaquinaIds
);

public record FinalizarSessaoRequest(
    int ProducaoFinal,
    int RefugoFinal,
    IList<LeituraExtraFinalRequest> Extras
);

public record LeituraExtraFinalRequest(Guid CampoMaquinaId, decimal Valor);