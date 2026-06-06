using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartLine.Infrastructure.Data;

public class SmartLineDbContextFactory : IDesignTimeDbContextFactory<SmartLineDbContext>
{
    public SmartLineDbContext CreateDbContext(string[] args)
    {
        var connectionString = "Host=localhost;Port=5432;Database=smartline;Username=smartline;Password=smartline123";

        var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SmartLineDbContext(optionsBuilder.Options);
    }
}
