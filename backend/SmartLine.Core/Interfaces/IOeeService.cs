using SmartLine.Core.Entities.Tenant;

namespace SmartLine.Core.Interfaces;

public interface IOeeService
{
    OeeResultado Calcular(Sessao sessao, decimal velocidadeNominal);
}

public record OeeResultado(
    double TempoTotalMs,
    double TempoPlanejadoMs,
    double TempoDisponivelMs,
    double TempoInternoMs,
    double TempoExternoMs,
    double TempoRodandoMs,
    double Disponibilidade,
    double Performance,
    double Qualidade,
    double Oee,
    int Producao,
    int Refugo,
    int NumParadas,
    int NumParadasInternas,
    int NumParadasExternas,
    int NumParadasPlanejadas
);
