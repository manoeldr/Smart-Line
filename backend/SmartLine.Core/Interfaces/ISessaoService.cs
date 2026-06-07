namespace SmartLine.Core.Interfaces;

public interface ISessaoService
{
    Task<SessaoDto?> AbrirAsync(Guid maquinaLinhaId, Guid usuarioId);
    Task<bool> FecharAsync(Guid sessaoId);
    Task<SessaoDto?> GetByIdAsync(Guid sessaoId);
}

public record SessaoDto(
    string Id,
    string MaquinaLinhaId,
    string UsuarioId,
    DateTime Inicio,
    DateTime? Fim,
    string Status
);
