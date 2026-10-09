using Microsoft.Extensions.Configuration;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

/// <summary>
/// A senha do Desenvolvedor não fica no banco: é derivada do segredo da licença e do MAC do
/// computador. No login, o hash gravado num usuário Desenvolvedor é ignorado.
/// </summary>
public class SenhaDesenvolvedorTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();
    private readonly IConfiguration _config = new ConfiguracaoFixa();

    // Só o JWT_SECRET, sem puxar o pacote de configuração para os testes.
    private sealed class ConfiguracaoFixa : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "JWT_SECRET" ? "segredo-de-teste-com-tamanho-suficiente-para-hmac-256" : null;
            set { }
        }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    public SenhaDesenvolvedorTests()
    {
        using var db = _banco.NovoContexto();
        db.Usuarios.AddRange(
            new Usuario { Id = Guid.NewGuid(), Nome = "Dev", Login = "desenvolvedor", SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha-no-banco"), Nivel = NivelUsuario.Desenvolvedor },
            new Usuario { Id = Guid.NewGuid(), Nome = "Admin", Login = "admin", SenhaHash = BCrypt.Net.BCrypt.HashPassword("admin123"), Nivel = NivelUsuario.Administrador });
        db.SaveChanges();
    }

    public void Dispose() => _banco.Dispose();

    [Fact]
    public void SenhaDerivada_EhDeterministica_EDiferenteDaChaveDeLicenca()
    {
        using var db = _banco.NovoContexto();
        var licenca = new LicencaService(db);

        var a = licenca.GerarSenhaDesenvolvedor("00-11-22-33-44-AA");
        var b = licenca.GerarSenhaDesenvolvedor("001122:3344aa");

        Assert.Equal(a, b); // separadores e maiúsculas não importam
        Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){5}$", a);
        Assert.NotEqual(licenca.GerarChaveEsperada("00-11-22-33-44-AA"), a);
        Assert.NotEqual(licenca.GerarSenhaDesenvolvedor("00-11-22-33-44-AB"), a);
    }

    [Fact]
    public async Task Desenvolvedor_EntraSoComASenhaDerivada_OHashDoBancoNaoVale()
    {
        await using var db = _banco.NovoContexto();
        var licenca = new LicencaService(db);
        var auth = new AuthService(db, _config, licenca);

        // A senha gravada no banco (que um Administrador ou alguém editando o .db poderia pôr) não entra.
        Assert.Null(await auth.LoginAsync("desenvolvedor", "senha-no-banco"));
        Assert.Null(await auth.LoginAsync("desenvolvedor", "qualquer-coisa"));

        // A derivada de uma placa física deste computador entra (o computador de teste pode não ter MAC).
        var mac = licenca.ObterMacsFisicos().FirstOrDefault(m => m != "SEM-MAC-DETECTADO");
        if (mac is not null)
        {
            var r = await auth.LoginAsync("desenvolvedor", licenca.GerarSenhaDesenvolvedor(mac).ToLowerInvariant());
            Assert.NotNull(r);
            Assert.Equal("Desenvolvedor", r.Usuario.Nivel);
        }

        // Os outros níveis continuam pela senha do banco.
        Assert.NotNull(await auth.LoginAsync("admin", "admin123"));
        Assert.Null(await auth.LoginAsync("admin", "errada"));
    }
}
