using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Tests.Infra;

/// <summary>
/// SQLite em arquivo temporário com o esquema real. Para testes com várias
/// threads gravando ao mesmo tempo (uma fila por máquina), como no app: cada
/// contexto abre a própria conexão. O <see cref="BancoEmMemoria"/> divide uma
/// conexão só, que não pode ser usada por duas threads.
/// </summary>
internal sealed class BancoEmArquivo : IDisposable
{
    private readonly string _caminho = Path.Combine(Path.GetTempPath(), $"smartline-teste-{Guid.NewGuid():N}.db");

    public BancoEmArquivo()
    {
        using var db = NovoContexto();
        db.Database.EnsureCreated();
    }

    public SmartLineDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<SmartLineDbContext>().UseSqlite($"Data Source={_caminho}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools(); // senão o Windows não deixa apagar
        foreach (var arquivo in new[] { _caminho, _caminho + "-wal", _caminho + "-shm", _caminho + "-journal" })
        {
            try { File.Delete(arquivo); }
            catch (IOException) { /* temporário: se sobrar, o sistema limpa */ }
        }
    }
}
