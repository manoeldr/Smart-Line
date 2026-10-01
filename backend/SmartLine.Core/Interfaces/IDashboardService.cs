namespace SmartLine.Core.Interfaces;

public interface IDashboardService
{
    Task<IList<MaquinaDashboardDto>> GetDashboardLinhaAsync(Guid linhaId, DateTime inicio, DateTime fim);

    /// <summary>
    /// Visão da linha inteira, sobre as mesmas sessões dos cards (a em andamento de cada
    /// máquina, senão a última do período): OEE e indicadores pela máquina crítica (a de pior
    /// OEE, se houver mais de uma) e paradas somadas de todas as máquinas.
    /// </summary>
    Task<LinhaDashboardDto> GetLinhaGeralAsync(Guid linhaId, DateTime inicio, DateTime fim);
}

/// <summary>Visão geral da linha no Dashboard.</summary>
/// <param name="MaquinaReferencia">Máquina de onde vêm OEE, disponibilidade, eficiência, qualidade e produção.</param>
/// <param name="ReferenciaCritica">
/// A referência é uma máquina marcada como crítica. Falso = nenhuma crítica com sessão no período:
/// vale a de pior OEE entre as que medem produção.
/// </param>
/// <param name="Producao">Produção da máquina de referência (o que a linha entregou).</param>
/// <param name="RefugoTotal">Refugo somado de todas as máquinas.</param>
/// <param name="TempoParadoTotalMs">Tempo parado somado de todas as máquinas (a parada em curso conta até agora).</param>
/// <param name="NumParadas">Paradas somadas de todas as máquinas.</param>
/// <param name="ProducaoPorHora">Gráfico de produção da máquina de referência (o mesmo do detalhe dela).</param>
public record LinhaDashboardDto(
    string? MaquinaReferencia,
    bool ReferenciaCritica,
    double? Oee,
    double Disponibilidade,
    double? Performance,
    double Qualidade,
    int Producao,
    int RefugoTotal,
    double TempoParadoTotalMs,
    int NumParadas,
    IList<MaquinaResumoLinhaDto> Maquinas,
    IList<ParadaLinhaPorMotivoDto> ParadasPorMotivo,
    IList<ParadaLinhaPorHoraDto> ParadasPorHora,
    IList<PontoProducaoDto> ProducaoPorHora
);

/// <summary>Uma máquina na visão da linha (sem sessão no período: tudo zero).</summary>
/// <param name="Referencia">É a máquina de onde vem o OEE da linha.</param>
public record MaquinaResumoLinhaDto(
    string MaquinaLinhaId,
    string Nome,
    bool Critica,
    bool Referencia,
    bool TemSessao,
    bool AoVivo,
    double? Oee,
    int Producao,
    int Refugo,
    double TempoParadoMs,
    int NumParadas
);

/// <summary>Tempo parado de uma máquina (numa hora ou num motivo).</summary>
public record TempoParadoMaquinaDto(string MaquinaLinhaId, double DuracaoMs);

/// <summary>Um motivo somado em todas as máquinas (motivos com o mesmo nome e tipo juntos), maior primeiro.</summary>
public record ParadaLinhaPorMotivoDto(string Motivo, string Tipo, double DuracaoMs, int Quantidade, IList<TempoParadoMaquinaDto> PorMaquina);

/// <summary>Tempo parado numa hora (Hora = início da hora), por máquina.</summary>
public record ParadaLinhaPorHoraDto(DateTime Hora, IList<TempoParadoMaquinaDto> PorMaquina);

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
