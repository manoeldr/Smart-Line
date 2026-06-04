using Microsoft.EntityFrameworkCore;

namespace SmartLine.Infrastructure.Data;

public class SmartLineDbContext : DbContext
{
    public SmartLineDbContext(DbContextOptions<SmartLineDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}