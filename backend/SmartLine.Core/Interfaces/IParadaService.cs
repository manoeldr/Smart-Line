using SmartLine.Core.Entities.Tenant;

namespace SmartLine.Core.Interfaces;

public interface IParadaService
{
    double? CalcularMtbf(IList<Parada> paradas);
    double? CalcularMttr(IList<Parada> paradas);
    IList<ParadaPorMotivo> AgruparPorMotivo(IList<Parada> paradas, double tempoTotalMs);
}

public record ParadaPorMotivo(
    string Motivo,
    string Tipo,
    int Count,
    double TotalMs,
    double Percentual
);
