namespace SmartLine.Core.Interfaces;

public interface IClienteService
{
    Task<IList<ClienteDto>> GetAllAsync();
    Task<ClienteDto?> GetByIdAsync(Guid id);
    Task<IList<LinhaOverviewDto>> GetLinhasAsync(Guid clienteId);

    /// <summary>
    /// Dias (no fuso deste computador, "yyyy-MM-dd") em que alguma máquina do cliente teve sessão,
    /// Manual ou Semi Automático; a sessão que atravessa dias conta em todos eles. Com
    /// <paramref name="linhaId"/>, só as máquinas daquela linha.
    /// </summary>
    Task<IList<string>> GetDatasComSessaoAsync(Guid clienteId, Guid? linhaId = null);
}

public record ClienteDto(
    string Id,
    string Nome,
    string? Estado,
    bool Ativo
);

public record LinhaOverviewDto(
    string Id,
    string ClienteId,
    string Nome,
    bool Ativo,
    IList<MaquinaLinhaOverviewDto> Maquinas
);

/// <param name="AcompanhamentoId">Coleta automática (Semi Automático) ligada na máquina; nulo no Manual ou sem sessão.</param>
/// <param name="MotivoParadaAtual">Motivo da parada em curso; nulo se rodando ou parada sem motivo.</param>
/// <param name="ParadasSemMotivo">Coleta automática: paradas da sessão do dia ainda sem motivo.</param>
/// <param name="EnderecoIpWise">IP do WISE da coleta Semi Automática em andamento; nulo sem coleta.</param>
/// <param name="SituacaoColeta">
/// Coleta automática, preenchido pela API a partir do motor: Rodando, Parada,
/// SemComunicacao ou AguardandoPrimeiraAmostra.
/// </param>
/// <param name="WiseConectado">Preenchido pela API (broker) quando a máquina tem WISE.</param>
/// <param name="SemComunicacaoMinutos">Preenchido pela API: há quantos minutos está sem comunicação.</param>
public record MaquinaLinhaOverviewDto(
    string Id,
    string LinhaId,
    string MaquinaId,
    string MaquinaNome,
    string TipoColeta,
    decimal VelocidadeNominal,
    bool Critica,
    bool MedeProducao,
    int Ordem,
    bool Ativo,
    string Status,
    double? Oee,
    bool SessaoAtiva,
    string? SessaoAtivaId,
    DateTime? UltimaSessaoFim,
    string? AcompanhamentoId = null,
    string? MotivoParadaAtual = null,
    int ParadasSemMotivo = 0,
    string? EnderecoIpWise = null,
    string? SituacaoColeta = null,
    bool? WiseConectado = null,
    int? SemComunicacaoMinutos = null
);