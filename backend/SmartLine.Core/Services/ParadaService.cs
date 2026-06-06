using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;

namespace SmartLine.Core.Services;

public class ParadaService : IParadaService
{
    public double? CalcularMtbf(IList<Parada> paradas)
    {
        // Apenas paradas finalizadas, ordenadas por início
        var finalizadas = paradas
            .Where(p => p.Fim.HasValue)
            .OrderBy(p => p.Inicio)
            .ToList();

        if (finalizadas.Count < 2)
            return null;

        // Intervalos de funcionamento entre paradas consecutivas
        var intervalos = new List<double>();
        for (int i = 1; i < finalizadas.Count; i++)
        {
            var intervalo = (finalizadas[i].Inicio - finalizadas[i - 1].Fim!.Value).TotalMilliseconds;
            if (intervalo > 0)
                intervalos.Add(intervalo);
        }

        if (intervalos.Count == 0)
            return null;

        return Math.Round(intervalos.Average());
    }

    public double? CalcularMttr(IList<Parada> paradas)
    {
        // Apenas paradas internas finalizadas
        var internasFinalizadas = paradas
            .Where(p => p.Fim.HasValue && p.Motivo.Tipo == TipoParada.Interna)
            .ToList();

        if (internasFinalizadas.Count == 0)
            return null;

        var mediaMs = internasFinalizadas
            .Average(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        return Math.Round(mediaMs);
    }

    public IList<ParadaPorMotivo> AgruparPorMotivo(IList<Parada> paradas, double tempoTotalMs)
    {
        var finalizadas = paradas
            .Where(p => p.Fim.HasValue)
            .ToList();

        var agrupadas = finalizadas
            .GroupBy(p => p.Motivo.Nome)
            .Select(g => new
            {
                Motivo = g.Key,
                Tipo = g.First().Motivo.Tipo.ToString(),
                Count = g.Count(),
                TotalMs = g.Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds)
            })
            .OrderByDescending(g => g.TotalMs)
            .ToList();

        return agrupadas.Select(g => new ParadaPorMotivo(
            Motivo: g.Motivo,
            Tipo: g.Tipo,
            Count: g.Count,
            TotalMs: Math.Round(g.TotalMs),
            Percentual: tempoTotalMs > 0
                ? Math.Round(g.TotalMs / tempoTotalMs * 100, 1)
                : 0
        )).ToList();
    }
}
