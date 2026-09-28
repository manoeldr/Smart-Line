using SmartLine.Core.Enums;
using SmartLine.Core.Iot;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Liga e desliga a coleta automática (Semi Automático) nas máquinas.
/// </summary>
public interface IAcompanhamentoService
{
    /// <summary>
    /// Inicia a coleta numa máquina. Uma por vez; várias podem rodar ao mesmo
    /// tempo, inclusive do mesmo usuário, e cada máquina da linha é independente
    /// (outras podem estar em medição Manual).
    /// </summary>
    Task<ResultadoIniciarAcompanhamento> IniciarAsync(
        Guid usuarioId,
        IniciarAcompanhamentoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finaliza a coleta: fecha a sessão do dia, a parada e o período sem
    /// comunicação que estiverem abertos. A máquina fica sem coleta até alguém
    /// iniciar de novo.
    /// </summary>
    /// <param name="podeFinalizarDeOutros">Administrador/Desenvolvedor (decidido no controller).</param>
    Task<ResultadoFinalizacao> FinalizarAsync(
        Guid acompanhamentoId,
        Guid usuarioId,
        bool podeFinalizarDeOutros,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monta a configuração de um acompanhamento em andamento (canais,
    /// multiplicadores, Z copiado no início, regras vigentes da máquina). Usado
    /// pelo coletor ao iniciar e ao retomar depois de reinício.
    /// </summary>
    /// <exception cref="InvalidOperationException">Acompanhamento inexistente ou com dados inconsistentes.</exception>
    Task<ConfiguracaoColetaIot> CarregarConfiguracaoAsync(
        Guid acompanhamentoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Coletas em andamento como estão gravadas: quem, onde, desde quando, o que
    /// lê, sessão do dia, produção já consolidada, parada aberta e falta de
    /// comunicação. O painel ao vivo junta isto com o estado em memória do motor.
    /// </summary>
    /// <param name="maquinaLinhaId">Só a desta máquina; nulo = todas.</param>
    Task<IReadOnlyList<ColetaIotResumoDto>> ListarEmAndamentoAsync(
        Guid? maquinaLinhaId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Máquina a iniciar e o que ler dela.</summary>
/// <param name="VelocidadeNominal">Nulo = a cadastrada na máquina da linha.</param>
/// <param name="SobreVelocidade">Nulo = a cadastrada na máquina da linha.</param>
/// <param name="Canais">Canais marcados na medição (contadores e sensores).</param>
public record IniciarAcompanhamentoRequest(
    Guid MaquinaLinhaId,
    decimal? VelocidadeNominal,
    decimal? SobreVelocidade,
    IList<CanalMedicaoRequest> Canais);

/// <param name="Multiplicador">Garrafas por pulso; ignorado (1) nos sensores de estado.</param>
public record CanalMedicaoRequest(CanalWise Canal, int Multiplicador = 1);

/// <summary>Resultado de iniciar: <see cref="Iniciado"/> no sucesso, <see cref="Erro"/> na recusa.</summary>
public record ResultadoIniciarAcompanhamento(AcompanhamentoIniciadoDto? Iniciado, string? Erro)
{
    public bool Sucesso => Iniciado is not null;
}

public record AcompanhamentoIniciadoDto(Guid AcompanhamentoId, Guid MaquinaLinhaId, Guid SessaoId);

/// <param name="EnderecoIp">IP do WISE da máquina; nulo se não houver WISE cadastrado.</param>
/// <param name="ProducaoConsolidada">Garrafas já gravadas na sessão do dia (última leitura).</param>
/// <param name="UltimaConsolidacao">Hora da última leitura gravada.</param>
/// <param name="SemComunicacaoDesde">Início do período sem comunicação em aberto, se houver.</param>
public record ColetaIotResumoDto(
    Guid AcompanhamentoId,
    Guid MaquinaLinhaId,
    string Maquina,
    Guid LinhaId,
    string Linha,
    string Cliente,
    Guid UsuarioId,
    string Usuario,
    DateTime IniciadoEm,
    int TempoDeteccaoParadaSegundos,
    IReadOnlyList<CanalMedicaoRequest> Canais,
    string? EnderecoIp,
    Guid? SessaoId,
    DateTime? SessaoInicio,
    decimal VelocidadeNominal,
    long ProducaoConsolidada,
    long RefugoConsolidado,
    DateTime? UltimaConsolidacao,
    ParadaAbertaDto? ParadaAberta,
    DateTime? SemComunicacaoDesde);

/// <param name="Motivo">Nome do motivo; nulo = não classificada (conta como Interna).</param>
public record ParadaAbertaDto(Guid ParadaId, DateTime Inicio, Guid? MotivoId, string? Motivo, TipoParada Tipo);

public enum ResultadoFinalizacao
{
    Finalizado,
    NaoEncontrado,
    JaFinalizado,
    SemPermissao
}
