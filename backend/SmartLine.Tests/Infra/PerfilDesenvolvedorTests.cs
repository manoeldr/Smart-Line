using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

/// <summary>
/// O perfil Desenvolvedor é reservado: um Administrador não vê, não cria, não edita, não troca a
/// senha, não desativa um Desenvolvedor, não promove ninguém (nem a si) a Desenvolvedor, e não
/// leva nem traz Desenvolvedores pela exportação/importação.
/// </summary>
public class PerfilDesenvolvedorTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _dev = Guid.NewGuid();
    private readonly string _hashDev = BCrypt.Net.BCrypt.HashPassword("segredo-do-dev");

    public PerfilDesenvolvedorTests()
    {
        using var db = _banco.NovoContexto();
        db.Usuarios.AddRange(
            new Usuario { Id = _admin, Nome = "Admin", Login = "admin", SenhaHash = "x", Nivel = NivelUsuario.Administrador },
            new Usuario { Id = _dev, Nome = "Dev", Login = "dev", SenhaHash = _hashDev, Nivel = NivelUsuario.Desenvolvedor });
        db.SaveChanges();
    }

    public void Dispose() => _banco.Dispose();

    private ConfiguracaoService Servico(SmartLine.Infrastructure.Data.SmartLineDbContext db) => new(db, new RegrasPadraoService(db));

    private static EditarUsuarioRequest Editar(string nivel, string? senha = null) => new("Nome", "login-x", senha, nivel, true, null);

    [Fact]
    public async Task Administrador_NaoVeOsDesenvolvedores_EODesenvolvedorVe()
    {
        await using var db = _banco.NovoContexto();
        var s = Servico(db);

        Assert.Equal(new[] { "admin" }, (await s.GetUsuariosAsync(chamadorDesenvolvedor: false)).Select(u => u.Login));
        Assert.Equal(new[] { "admin", "dev" }, (await s.GetUsuariosAsync(chamadorDesenvolvedor: true)).Select(u => u.Login).Order());
    }

    [Fact]
    public async Task Administrador_NaoMexeNoDesenvolvedor_NemPromoveNinguem()
    {
        await using var db = _banco.NovoContexto();
        var s = Servico(db);

        await Assert.ThrowsAsync<PerfilReservadoException>(() => s.EditarUsuarioAsync(_dev, Editar("Desenvolvedor", senha: "nova"), chamadorDesenvolvedor: false));
        await Assert.ThrowsAsync<PerfilReservadoException>(() => s.EditarUsuarioAsync(_dev, Editar("Administrador"), chamadorDesenvolvedor: false));
        await Assert.ThrowsAsync<PerfilReservadoException>(() => s.EditarUsuarioAsync(_admin, Editar("Desenvolvedor"), chamadorDesenvolvedor: false));
        await Assert.ThrowsAsync<PerfilReservadoException>(() => s.DeletarUsuarioAsync(_dev, chamadorDesenvolvedor: false));
        await Assert.ThrowsAsync<PerfilReservadoException>(() => s.CriarUsuarioAsync(new CriarUsuarioRequest("Outro", "outro", "123", "Desenvolvedor", null), chamadorDesenvolvedor: false));

        await using var confere = _banco.NovoContexto();
        var dev = await confere.Usuarios.SingleAsync(u => u.Id == _dev);
        Assert.Equal((_hashDev, NivelUsuario.Desenvolvedor, true, "dev"), (dev.SenhaHash, dev.Nivel, dev.Ativo, dev.Login));
        Assert.Equal(NivelUsuario.Administrador, (await confere.Usuarios.SingleAsync(u => u.Id == _admin)).Nivel);
    }

    [Fact]
    public async Task Desenvolvedor_PodeTudo()
    {
        await using var db = _banco.NovoContexto();
        var s = Servico(db);

        await s.EditarUsuarioAsync(_dev, Editar("Desenvolvedor", senha: "nova"), chamadorDesenvolvedor: true);
        var criado = await s.CriarUsuarioAsync(new CriarUsuarioRequest("Outro", "outro", "123", "Desenvolvedor", null), chamadorDesenvolvedor: true);

        await using var confere = _banco.NovoContexto();
        Assert.NotEqual(_hashDev, (await confere.Usuarios.SingleAsync(u => u.Id == _dev)).SenhaHash);
        Assert.Equal(NivelUsuario.Desenvolvedor, (await confere.Usuarios.SingleAsync(u => u.Id == Guid.Parse(criado.Id))).Nivel);
    }

    [Fact]
    public async Task ExportacaoEImportacao_DeQuemNaoEhDesenvolvedor_NaoLevamNemTrazemDesenvolvedor()
    {
        byte[] pacoteAdmin, pacoteDev;
        await using (var db = _banco.NovoContexto())
        {
            var e = new ExportImportService(db);
            pacoteAdmin = await e.ExportarAsync(new ExportOpcoes(false, false, false, true), chamadorDesenvolvedor: false);
            pacoteDev = await e.ExportarAsync(new ExportOpcoes(false, false, false, true), chamadorDesenvolvedor: true);
        }

        using var destino = new BancoEmMemoria();
        await using (var db = destino.NovoContexto())
        {
            // O pacote do Admin não traz o Dev; o do Dev traz, mas um Admin importando o ignora.
            Assert.Equal(1, (await new ExportImportService(db).PreviaImportacaoAsync(pacoteAdmin)).Usuarios);
            Assert.Equal(2, (await new ExportImportService(db).PreviaImportacaoAsync(pacoteDev)).Usuarios);
            await new ExportImportService(db).ImportarAsync(pacoteDev, chamadorDesenvolvedor: false);
        }
        await using (var db = destino.NovoContexto())
            Assert.Equal(new[] { "admin" }, (await db.Usuarios.Select(u => u.Login).ToListAsync()).Order());

        // Importado por um Desenvolvedor, o Dev entra.
        await using (var db = destino.NovoContexto())
            await new ExportImportService(db).ImportarAsync(pacoteDev, chamadorDesenvolvedor: true);
        await using (var db = destino.NovoContexto())
            Assert.Equal(new[] { "admin", "dev" }, (await db.Usuarios.Select(u => u.Login).ToListAsync()).Order());
    }
}
