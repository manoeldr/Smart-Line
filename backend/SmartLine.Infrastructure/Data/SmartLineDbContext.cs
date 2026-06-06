using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;

namespace SmartLine.Infrastructure.Data;

public class SmartLineDbContext : DbContext
{
    public SmartLineDbContext(DbContextOptions<SmartLineDbContext> options)
        : base(options)
    {
    }

    // Global
    public DbSet<Maquina> Maquinas => Set<Maquina>();
    public DbSet<MotivoParada> MotivosParada => Set<MotivoParada>();
    public DbSet<TipoMedicao> TiposMedicao => Set<TipoMedicao>();

    // Tenant
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Linha> Linhas => Set<Linha>();
    public DbSet<MaquinaLinha> MaquinasLinha => Set<MaquinaLinha>();
    public DbSet<Sessao> Sessoes => Set<Sessao>();
    public DbSet<Producao> Producoes => Set<Producao>();
    public DbSet<Parada> Paradas => Set<Parada>();
    public DbSet<Medicao> Medicoes => Set<Medicao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartLineDbContext).Assembly);
    }
}