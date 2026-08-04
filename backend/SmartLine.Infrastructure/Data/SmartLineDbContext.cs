using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<Licenca> Licencas => Set<Licenca>();

    // Tenant
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Linha> Linhas => Set<Linha>();
    public DbSet<MaquinaLinha> MaquinasLinha => Set<MaquinaLinha>();
    public DbSet<Sessao> Sessoes => Set<Sessao>();
    public DbSet<Producao> Producoes => Set<Producao>();
    public DbSet<Parada> Paradas => Set<Parada>();
    public DbSet<Medicao> Medicoes => Set<Medicao>();
    public DbSet<CampoMaquina> CamposMaquina => Set<CampoMaquina>();
    public DbSet<SessaoCampo> SessoesCampo => Set<SessaoCampo>();
    public DbSet<LeituraExtra> LeiturasExtra => Set<LeituraExtra>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartLineDbContext).Assembly);

        // SQLite não guarda informação de fuso horário nas colunas de data/hora — ao ler de volta,
        // o EF Core marca o DateTime como "Unspecified", o que faz o JSON ir sem o sufixo "Z" e o
        // navegador interpretar como horário local, quebrando cronômetros e cálculos de tempo.
        // Como toda gravação no sistema usa DateTime.UtcNow, forçamos aqui que toda LEITURA
        // também seja marcada como UTC, em todas as entidades, de uma vez só.
        var conversorDateTime = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var conversorDateTimeNulo = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(conversorDateTime);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(conversorDateTimeNulo);
                }
            }
        }
    }
}