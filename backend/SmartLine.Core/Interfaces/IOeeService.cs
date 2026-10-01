using SmartLine.Core.Entities.Tenant;

namespace SmartLine.Core.Interfaces;

public interface IOeeService
{
    // medeProducao: quando false, a máquina não coleta leitura de produção — Performance e OEE
    // ficam nulos (indisponíveis pra calcular), em vez de um valor enganoso.
    // semComunicacao: períodos da máquina sem comunicação (coleta automática). O tempo deles sai
    // do cálculo e a produção feita neles (leituras marcadas) não entra na performance nem na qualidade.
    OeeResultado Calcular(Sessao sessao, decimal velocidadeNominal, bool medeProducao = true,
        IEnumerable<PeriodoSemComunicacao>? semComunicacao = null);
}

public record OeeResultado(
    double TempoTotalMs,
    double TempoPlanejadoMs,
    double TempoDisponivelMs,
    double TempoInternoMs,
    double TempoExternoMs,
    double TempoRodandoMs,
    double Disponibilidade,
    double? Performance,
    double Qualidade,
    double? Oee,
    int Producao,
    int Refugo,
    int NumParadas,
    int NumParadasInternas,
    int NumParadasExternas,
    int NumParadasPlanejadas,
    double TempoSemComunicacaoMs = 0
);