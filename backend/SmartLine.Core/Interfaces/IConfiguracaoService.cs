namespace SmartLine.Core.Interfaces;

public interface IConfiguracaoService
{
    // Usuários
    /// <summary>
    /// Usuários. O perfil Desenvolvedor é reservado: só um Desenvolvedor vê, cria, edita, troca a senha
    /// ou desativa um usuário Desenvolvedor, e só ele dá esse nível a alguém. Um Administrador que
    /// tente recebe <see cref="PerfilReservadoException"/>.
    /// </summary>
    Task<IList<UsuarioConfDto>> GetUsuariosAsync(bool chamadorDesenvolvedor);
    Task<UsuarioConfDto> CriarUsuarioAsync(CriarUsuarioRequest req, bool chamadorDesenvolvedor);
    Task<UsuarioConfDto> EditarUsuarioAsync(Guid id, EditarUsuarioRequest req, bool chamadorDesenvolvedor);
    Task DeletarUsuarioAsync(Guid id, bool chamadorDesenvolvedor);

    // Clientes
    Task<IList<ClienteConfDto>> GetClientesAsync();
    Task<ClienteConfDto> CriarClienteAsync(CriarClienteRequest req);
    Task<ClienteConfDto> EditarClienteAsync(Guid id, EditarClienteRequest req);
    Task DeletarClienteAsync(Guid id);

    // Linhas
    Task<IList<LinhaConfDto>> GetLinhasAsync();
    Task<LinhaConfDto> CriarLinhaAsync(CriarLinhaRequest req);
    Task<LinhaConfDto> EditarLinhaAsync(Guid id, EditarLinhaRequest req);
    Task DeletarLinhaAsync(Guid id);

    // Máquinas
    Task<IList<MaquinaConfDto>> GetMaquinasAsync();
    Task<MaquinaConfDto> CriarMaquinaAsync(CriarMaquinaRequest req);
    Task<MaquinaConfDto> EditarMaquinaAsync(Guid id, EditarMaquinaRequest req);
    Task DeletarMaquinaAsync(Guid id);

    // Campos de Máquina
    Task<IList<CampoMaquinaDto>> GetCamposMaquinaAsync(Guid maquinaId);
    Task<CampoMaquinaDto> CriarCampoMaquinaAsync(Guid maquinaId, CriarCampoMaquinaRequest req);
    Task<CampoMaquinaDto> EditarCampoMaquinaAsync(Guid id, EditarCampoMaquinaRequest req);
    Task DeletarCampoMaquinaAsync(Guid id);
}

// ── DTOs ──────────────────────────────────────────────────────
/// <summary>Tentativa de mexer no perfil Desenvolvedor sem ser Desenvolvedor.</summary>
public class PerfilReservadoException : Exception
{
    public PerfilReservadoException() : base("O perfil Desenvolvedor só pode ser alterado por um Desenvolvedor.") { }
}

public record UsuarioConfDto(string Id, string Nome, string Login, string Nivel, bool Ativo, string? ClienteId, string? ClienteNome);
public record ClienteConfDto(string Id, string Nome, string? Estado, bool Ativo);
public record LinhaConfDto(string Id, string Nome, string ClienteId, string ClienteNome, bool Ativo);
public record MaquinaConfDto(string Id, string Nome, string? Fabricante, string? Descricao, bool Ativo);
public record CampoMaquinaDto(string Id, string MaquinaId, string Nome, string? Unidade, int Ordem, bool Ativo);

// ── Requests ──────────────────────────────────────────────────
public record CriarUsuarioRequest(string Nome, string Login, string Senha, string Nivel, string? ClienteId);
public record EditarUsuarioRequest(string Nome, string Login, string? Senha, string Nivel, bool Ativo, string? ClienteId);
public record CriarClienteRequest(string Nome, string? Estado);
public record EditarClienteRequest(string Nome, string? Estado, bool Ativo);
public record CriarLinhaRequest(string Nome, string ClienteId);
public record EditarLinhaRequest(string Nome, bool Ativo);
public record CriarMaquinaRequest(string Nome, string? Fabricante, string? Descricao);
public record EditarMaquinaRequest(string Nome, string? Fabricante, string? Descricao, bool Ativo);
public record CriarCampoMaquinaRequest(string Nome, string? Unidade, int Ordem);
public record EditarCampoMaquinaRequest(string Nome, string? Unidade, int Ordem, bool Ativo);