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

public enum ResultadoFinalizacao
{
    Finalizado,
    NaoEncontrado,
    JaFinalizado,
    SemPermissao
}
