namespace SmartLine.Core.Interfaces;

public interface IDashboardService
{
    Task<IList<MaquinaDashboardDto>> GetDashboardLinhaAsync(Guid linhaId, DateTime inicio, DateTime fim);
}

/// <param name="AoVivo">A máquina tem uma sessão em andamento no período (Manual ou Semi Automático).</param>
/// <param name="SituacaoAoVivo">
/// Com sessão em andamento: Rodando ou Parada (parada aberta). No Semi Automático a API troca
/// pela situação do motor da coleta (inclui SemComunicacao e AguardandoPrimeiraAmostra).
/// </param>
/// <param name="AcompanhamentoId">Coleta Semi Automática da sessão em andamento, se houver.</param>
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
    double TempoParadoMs,
    bool AoVivo = false,
    string? SituacaoAoVivo = null,
    string? AcompanhamentoId = null
);