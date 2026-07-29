using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartLine.Infrastructure.Data;

// Usado pela ferramenta `dotnet ef` em tempo de design (criação de migrations).
// Usa o mesmo caminho absoluto (AppContext.BaseDirectory) que o Program.cs usa em runtime,
// para garantir que os dois sempre apontem para o mesmo arquivo de banco.
public class SmartLineDbContextFactory : IDesignTimeDbContextFactory<SmartLineDbContext>
{
    public SmartLineDbContext CreateDbContext(string[] args)
    {
        var caminhoDb = Path.Combine(AppContext.BaseDirectory, "smartline.db");
        var connectionString = $"Data Source={caminhoDb}";
        var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new SmartLineDbContext(optionsBuilder.Options);
    }
}