namespace SmartLine.Core.Interfaces;

public interface IDashboardService
{
    Task<IList<MaquinaDashboardDto>> GetDashboardLinhaAsync(Guid linhaId, DateTime inicio, DateTime fim);
}

public record MaquinaDashboardDto(
    string MaquinaLinhaId,
    string MaquinaNome,
    bool Critica,
    double? Oee,
    double Disponibilidade,
    double? Performance,
    double Qualidade,
    int Producao,
    int Refugo,
    int NumSessoes,
    double TempoRodandoMs,
    double TempoParadoMs
);