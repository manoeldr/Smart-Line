namespace SmartLine.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResultado?> LoginAsync(string login, string senha);
}

public record LoginResultado(string Token, UsuarioDto Usuario);

public record UsuarioDto(
    string Id,
    string? ClienteId,
    string Nome,
    string Login,
    string Nivel,
    bool Ativo
);
