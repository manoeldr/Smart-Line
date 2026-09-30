namespace SmartLine.Core.Interfaces;

public interface IDashboardService
{
    Task<IList<MaquinaDashboardDto>> GetDashboardLinhaAsync(Guid linhaId, DateTime inicio, DateTime fim);
}

/// <summary>
/// Card de uma máquina no Dashboard: valores exatos de uma sessão — a em andamento, se houver,
/// senão a última iniciada no período (a mesma do detalhe da máquina). Sem sessão, tudo zero.
/// </summary>
/// <param name="SessaoInicio">Início da sessão mostrada; nulo se não há sessão no período.</param>
/// <param name="SessaoFim">Fim da sessão mostrada; nulo se em andamento ou sem sessão.</param>
/// <param name="AoVivo">A sessão mostrada está em andamento (Manual ou Semi Automático).</param>
/// <param name="SituacaoAoVivo">
/// Em andamento: Rodando ou Parada (parada aberta). No Semi Automático a API troca pela
/// situação do motor da coleta (inclui SemComunicacao e AguardandoPrimeiraAmostra).
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
    double TempoRodandoMs,
    double TempoParadoMs,
    DateTime? SessaoInicio = null,
    DateTime? SessaoFim = null,
    bool AoVivo = false,
    string? SituacaoAoVivo = null,
    string? AcompanhamentoId = null
);
