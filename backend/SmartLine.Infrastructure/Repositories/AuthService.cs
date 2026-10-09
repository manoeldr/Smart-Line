using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SmartLine.Infrastructure.Repositories;

public class AuthService : IAuthService
{
    private readonly SmartLineDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILicencaService _licenca;

    public AuthService(SmartLineDbContext context, IConfiguration configuration, ILicencaService licenca)
    {
        _context = context;
        _configuration = configuration;
        _licenca = licenca;
    }

    public async Task<LoginResultado?> LoginAsync(string login, string senha)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Login == login && u.Ativo);

        if (usuario is null)
            return null;

        // O Desenvolvedor não tem senha no banco: entra com a senha derivada do segredo e do MAC
        // deste computador (gerada pelo Desenvolvedor na aba Licença ou no LicenseGenerator).
        // O hash gravado num usuário desse nível é ignorado, então editar o banco não adianta.
        var senhaCerta = usuario.Nivel == SmartLine.Core.Enums.NivelUsuario.Desenvolvedor
            ? _licenca.SenhaDesenvolvedorValida(senha)
            : BCrypt.Net.BCrypt.Verify(senha, usuario.SenhaHash);
        if (!senhaCerta)
            return null;

        var token = GerarToken(usuario);

        var dto = new UsuarioDto(
            Id: usuario.Id.ToString(),
            ClienteId: usuario.ClienteId?.ToString(),
            Nome: usuario.Nome,
            Login: usuario.Login,
            Nivel: usuario.Nivel.ToString(),
            Ativo: usuario.Ativo
        );

        return new LoginResultado(token, dto);
    }

    private string GerarToken(SmartLine.Core.Entities.Tenant.Usuario usuario)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? _configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET não configurado.");

        var expiresHours = int.TryParse(
            Environment.GetEnvironmentVariable("JWT_EXPIRES_HOURS") ?? "8", out var h) ? h : 8;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim("nome", usuario.Nome),
            new Claim("nivel", usuario.Nivel.ToString()),
            new Claim("clienteId", usuario.ClienteId?.ToString() ?? ""),
        };

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiresHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
