using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Infrastructure.Data;

var caminhoDb = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";
var connectionString = $"Data Source={caminhoDb}";

// 1) Remove o admin problemático via SQL bruto, filtrando só por Login
//    (evita comparar por Id, que é onde o formato incompatível quebra tudo).
using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "DELETE FROM Usuarios WHERE Login = 'admin';";
    var linhas = cmd.ExecuteNonQuery();
    Console.WriteLine($"Removidos {linhas} registro(s) de admin antigo.");
}

// 2) Cria o novo admin inteiramente pelo EF Core — garante que o Id seja salvo
//    no formato que o próprio EF Core reconhece de forma consistente.
var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
optionsBuilder.UseSqlite(connectionString);

using var context = new SmartLineDbContext(optionsBuilder.Options);

var novoAdmin = new Usuario
{
    Id = Guid.NewGuid(),
    ClienteId = null,
    Nome = "Admin",
    Login = "admin",
    SenhaHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
    Nivel = NivelUsuario.Administrador,
    Ativo = true,
    CriadoEm = DateTime.UtcNow,
};

context.Usuarios.Add(novoAdmin);
context.SaveChanges();

Console.WriteLine($"Admin criado via EF Core com sucesso! Id: {novoAdmin.Id}");