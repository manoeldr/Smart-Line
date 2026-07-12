using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ConfiguracaoService : IConfiguracaoService
{
    private readonly SmartLineDbContext _context;

    public ConfiguracaoService(SmartLineDbContext context)
    {
        _context = context;
    }

    // ── Usuários ──────────────────────────────────────────────
    public async Task<IList<UsuarioConfDto>> GetUsuariosAsync() =>
        await _context.Usuarios
            .Include(u => u.Cliente)
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioConfDto(
                u.Id.ToString(), u.Nome, u.Login,
                u.Nivel.ToString(), u.Ativo,
                u.ClienteId != null ? u.ClienteId.ToString() : null,
                u.Cliente != null ? u.Cliente.Nome : null
            ))
            .ToListAsync();

    public async Task<UsuarioConfDto> CriarUsuarioAsync(CriarUsuarioRequest req)
    {
        var nivel = Enum.Parse<NivelUsuario>(req.Nivel);
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = req.Nome,
            Login = req.Login,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha),
            Nivel = nivel,
            ClienteId = req.ClienteId is not null ? Guid.Parse(req.ClienteId) : null,
            Ativo = true,
        };
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return new UsuarioConfDto(
            usuario.Id.ToString(), usuario.Nome, usuario.Login,
            usuario.Nivel.ToString(), usuario.Ativo,
            usuario.ClienteId?.ToString(), null
        );
    }

    public async Task<UsuarioConfDto> EditarUsuarioAsync(Guid id, EditarUsuarioRequest req)
    {
        var usuario = await _context.Usuarios.FindAsync(id)
            ?? throw new Exception("Usuário não encontrado");
        usuario.Nome = req.Nome;
        usuario.Login = req.Login;
        usuario.Nivel = Enum.Parse<NivelUsuario>(req.Nivel);
        usuario.Ativo = req.Ativo;
        usuario.ClienteId = req.ClienteId is not null ? Guid.Parse(req.ClienteId) : null;
        if (!string.IsNullOrWhiteSpace(req.Senha))
            usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha);
        await _context.SaveChangesAsync();
        return new UsuarioConfDto(
            usuario.Id.ToString(), usuario.Nome, usuario.Login,
            usuario.Nivel.ToString(), usuario.Ativo,
            usuario.ClienteId?.ToString(), null
        );
    }

    public async Task DeletarUsuarioAsync(Guid id)
    {
        var usuario = await _context.Usuarios.FindAsync(id)
            ?? throw new Exception("Usuário não encontrado");
        usuario.Ativo = false;
        await _context.SaveChangesAsync();
    }

    // ── Clientes ──────────────────────────────────────────────
    public async Task<IList<ClienteConfDto>> GetClientesAsync() =>
        await _context.Clientes
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteConfDto(c.Id.ToString(), c.Nome, c.Estado, c.Ativo))
            .ToListAsync();

    public async Task<ClienteConfDto> CriarClienteAsync(CriarClienteRequest req)
    {
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = req.Nome,
            Estado = req.Estado,
            Ativo = true
        };
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return new ClienteConfDto(cliente.Id.ToString(), cliente.Nome, cliente.Estado, cliente.Ativo);
    }

    public async Task<ClienteConfDto> EditarClienteAsync(Guid id, EditarClienteRequest req)
    {
        var cliente = await _context.Clientes.FindAsync(id)
            ?? throw new Exception("Cliente não encontrado");
        cliente.Nome = req.Nome;
        cliente.Estado = req.Estado;
        cliente.Ativo = req.Ativo;
        await _context.SaveChangesAsync();
        return new ClienteConfDto(cliente.Id.ToString(), cliente.Nome, cliente.Estado, cliente.Ativo);
    }

    public async Task DeletarClienteAsync(Guid id)
    {
        var cliente = await _context.Clientes.FindAsync(id)
            ?? throw new Exception("Cliente não encontrado");
        cliente.Ativo = false;
        await _context.SaveChangesAsync();
    }

    // ── Linhas ────────────────────────────────────────────────
    public async Task<IList<LinhaConfDto>> GetLinhasAsync() =>
        await _context.Linhas
            .Include(l => l.Cliente)
            .OrderBy(l => l.Nome)
            .Select(l => new LinhaConfDto(
                l.Id.ToString(), l.Nome,
                l.ClienteId.ToString(), l.Cliente.Nome, l.Ativo
            ))
            .ToListAsync();

    public async Task<LinhaConfDto> CriarLinhaAsync(CriarLinhaRequest req)
    {
        var linha = new Linha
        {
            Id = Guid.NewGuid(),
            Nome = req.Nome,
            ClienteId = Guid.Parse(req.ClienteId),
            Ativo = true
        };
        _context.Linhas.Add(linha);
        await _context.SaveChangesAsync();
        var cliente = await _context.Clientes.FindAsync(linha.ClienteId);
        return new LinhaConfDto(linha.Id.ToString(), linha.Nome, linha.ClienteId.ToString(), cliente?.Nome ?? "", linha.Ativo);
    }

    public async Task<LinhaConfDto> EditarLinhaAsync(Guid id, EditarLinhaRequest req)
    {
        var linha = await _context.Linhas.Include(l => l.Cliente).FirstOrDefaultAsync(l => l.Id == id)
            ?? throw new Exception("Linha não encontrada");
        linha.Nome = req.Nome;
        linha.Ativo = req.Ativo;
        await _context.SaveChangesAsync();
        return new LinhaConfDto(linha.Id.ToString(), linha.Nome, linha.ClienteId.ToString(), linha.Cliente.Nome, linha.Ativo);
    }

    public async Task DeletarLinhaAsync(Guid id)
    {
        var linha = await _context.Linhas.FindAsync(id)
            ?? throw new Exception("Linha não encontrada");
        linha.Ativo = false;
        await _context.SaveChangesAsync();
    }

    // ── Máquinas ──────────────────────────────────────────────
    public async Task<IList<MaquinaConfDto>> GetMaquinasAsync() =>
        await _context.Maquinas
            .OrderBy(m => m.Nome)
            .Select(m => new MaquinaConfDto(m.Id.ToString(), m.Nome, m.Fabricante, m.Descricao, m.Ativo))
            .ToListAsync();

    public async Task<MaquinaConfDto> CriarMaquinaAsync(CriarMaquinaRequest req)
    {
        var maquina = new Maquina
        {
            Id = Guid.NewGuid(),
            Nome = req.Nome,
            Fabricante = req.Fabricante,
            Descricao = req.Descricao,
            Ativo = true
        };
        _context.Maquinas.Add(maquina);
        await _context.SaveChangesAsync();
        return new MaquinaConfDto(maquina.Id.ToString(), maquina.Nome, maquina.Fabricante, maquina.Descricao, maquina.Ativo);
    }

    public async Task<MaquinaConfDto> EditarMaquinaAsync(Guid id, EditarMaquinaRequest req)
    {
        var maquina = await _context.Maquinas.FindAsync(id)
            ?? throw new Exception("Máquina não encontrada");
        maquina.Nome = req.Nome;
        maquina.Fabricante = req.Fabricante;
        maquina.Descricao = req.Descricao;
        maquina.Ativo = req.Ativo;
        await _context.SaveChangesAsync();
        return new MaquinaConfDto(maquina.Id.ToString(), maquina.Nome, maquina.Fabricante, maquina.Descricao, maquina.Ativo);
    }

    public async Task DeletarMaquinaAsync(Guid id)
    {
        var maquina = await _context.Maquinas.FindAsync(id)
            ?? throw new Exception("Máquina não encontrada");
        maquina.Ativo = false;
        await _context.SaveChangesAsync();
    }
}