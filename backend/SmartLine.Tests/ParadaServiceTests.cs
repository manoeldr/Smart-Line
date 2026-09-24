using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Services;

namespace SmartLine.Tests;

public class ParadaServiceTests
{
    private readonly ParadaService _sut = new();
    private static readonly DateTime T0 = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Parada NovaParada(int inicioMin, int fimMin, TipoParada? tipo, string? nome = null) => new()
    {
        Id = Guid.NewGuid(),
        Inicio = T0.AddMinutes(inicioMin),
        Fim = T0.AddMinutes(fimMin),
        Motivo = tipo is null ? null : new MotivoParada { Nome = nome ?? tipo.ToString()!, Tipo = tipo.Value }
    };

    [Fact]
    public void Mttr_ConsideraParadaSemMotivoComoInterna()
    {
        var paradas = new List<Parada>
        {
            NovaParada(0, 10, null),                 // 10 min, não classificada → Interna
            NovaParada(20, 50, TipoParada.Interna),  // 30 min
            NovaParada(60, 120, TipoParada.Externa)  // fora do MTTR
        };

        var mttr = _sut.CalcularMttr(paradas);

        Assert.Equal(TimeSpan.FromMinutes(20).TotalMilliseconds, mttr);
    }

    [Fact]
    public void AgruparPorMotivo_ParadaSemMotivo_NaoQuebraEAgrupaComoNaoClassificada()
    {
        var paradas = new List<Parada>
        {
            NovaParada(0, 10, null),
            NovaParada(20, 25, null),
            NovaParada(30, 40, TipoParada.Externa, "Falta de garrafas")
        };

        var grupos = _sut.AgruparPorMotivo(paradas, TimeSpan.FromHours(1).TotalMilliseconds);

        var naoClassificadas = Assert.Single(grupos, g => g.Motivo == ParadaService.MotivoNaoClassificada);
        Assert.Equal(2, naoClassificadas.Count);
        Assert.Equal(nameof(TipoParada.Interna), naoClassificadas.Tipo);
        Assert.Equal(TimeSpan.FromMinutes(15).TotalMilliseconds, naoClassificadas.TotalMs);
    }
}
