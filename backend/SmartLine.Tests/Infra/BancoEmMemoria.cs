using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Tests.Infra;

/// <summary>
/// SQLite em memória com o esquema real do app (mesmas constraints, índices e
/// chaves). A conexão fica aberta enquanto o objeto viver: fechar apaga o banco.
/// </summary>
internal sealed class BancoEmMemoria : IDisposable
{
    private readonly SqliteConnection _conexao;

    public BancoEmMemoria()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        using var db = NovoContexto();
        db.Database.EnsureCreated();
    }

    /// <summary>
    /// Contexto novo sobre o mesmo banco. Usar um para preparar e outro para
    /// verificar evita que o cache do EF esconda o que foi (ou não) gravado.
    /// </summary>
    public SmartLineDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<SmartLineDbContext>().UseSqlite(_conexao).Options);

    public void Dispose() => _conexao.Dispose();
}
