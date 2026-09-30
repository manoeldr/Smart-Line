using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

/// <summary>Gráficos de paradas do detalhe da máquina: por hora e por motivo.</summary>
public class GraficosParadasTests
{
    private static readonly DateTime T = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
    private static readonly MotivoParada Falta = new() { Id = Guid.NewGuid(), Nome = "Falta de garrafas", Tipo = TipoParada.Externa };
    private static readonly MotivoParada Setup = new() { Id = Guid.NewGuid(), Nome = "Setup", Tipo = TipoParada.Planejada };

    private static Parada Parada(int deMin, int? ateMin, MotivoParada? motivo = null) => new()
    {
        Id = Guid.NewGuid(),
        Inicio = T.AddMinutes(deMin),
        Fim = ateMin is null ? null : T.AddMinutes(ateMin.Value),
        MotivoId = motivo?.Id,
        Motivo = motivo
    };

    private static double Min(double ms) => ms / 60000;

    [Fact]
    public void PorHora_UmaEntradaPorHora_ParadaQueAtravessaAHoraEntraEmCada_EmCursoAteAgora()
    {
        var paradas = new[]
        {
            Parada(10, 20),            // 10h: 10 min interna (sem motivo)
            Parada(50, 70, Falta),     // 10h: 10 min externa; 11h: 10 min externa
            Parada(150, null, Setup),  // 12h: em curso desde 12:30, até agora (12:45)
        };

        var horas = SessaoDetalheService.ParadasPorHora(paradas, T, T.AddMinutes(165));

        Assert.Equal(new[] { T, T.AddHours(1), T.AddHours(2) }, horas.Select(h => h.Hora));
        Assert.Equal((10d, 10d, 0d), (Min(horas[0].InternaMs), Min(horas[0].ExternaMs), Min(horas[0].PlanejadaMs)));
        Assert.Equal((0d, 10d, 0d), (Min(horas[1].InternaMs), Min(horas[1].ExternaMs), Min(horas[1].PlanejadaMs)));
        Assert.Equal((0d, 0d, 15d), (Min(horas[2].InternaMs), Min(horas[2].ExternaMs), Min(horas[2].PlanejadaMs)));
    }

    [Fact]
    public void PorHora_HorasSemParadaAparecemZeradas()
    {
        var horas = SessaoDetalheService.ParadasPorHora([], T.AddMinutes(30), T.AddHours(3));

        Assert.Equal(new[] { T, T.AddHours(1), T.AddHours(2) }, horas.Select(h => h.Hora));
        Assert.All(horas, h => Assert.Equal(0d, h.InternaMs + h.ExternaMs + h.PlanejadaMs));
    }

    [Fact]
    public void PorMotivo_DoMaiorTempoParaOMenor_SemMotivoNumGrupoSo_EmCursoAteAgora()
    {
        var paradas = new[]
        {
            Parada(0, 5),              // sem motivo: 5
            Parada(10, 30, Falta),     // falta: 20
            Parada(40, 45),            // sem motivo: 5
            Parada(60, 70, Falta),     // falta: 10
            Parada(80, null, Setup),   // setup: em curso até 120 → 40
        };

        var motivos = SessaoDetalheService.ParadasPorMotivo(paradas, T.AddMinutes(120));

        Assert.Equal(
            new[] { ("Setup", "Planejada", 40d, 1), ("Falta de garrafas", "Externa", 30d, 2), ("Sem motivo", "Interna", 10d, 2) },
            motivos.Select(m => (m.Motivo, m.Tipo, Min(m.DuracaoMs), m.Quantidade)));
        Assert.Null(motivos[2].MotivoId);
        Assert.Equal(Falta.Id.ToString(), motivos[1].MotivoId);
    }
}
