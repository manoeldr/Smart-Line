namespace SmartLine.Core.Interfaces;

public interface IConfiguracaoService
{
    // Usuários
    Task<IList<UsuarioConfDto>> GetUsuariosAsync();
    Task<UsuarioConfDto> CriarUsuarioAsync(CriarUsuarioRequest req);
    Task<UsuarioConfDto> EditarUsuarioAsync(Guid id, EditarUsuarioRequest req);
    Task DeletarUsuarioAsync(Guid id);

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