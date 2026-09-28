using SmartLine.Core.Enums;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Paradas da coleta automática vistas por quem classifica: as que ficaram
/// sem causa (pendentes) e a troca manual do motivo, sempre com histórico.
/// </summary>
public interface IClassificacaoParadaService
{
    /// <summary>
    /// Paradas não classificadas das coletas automáticas (Semi Automático e
    /// Automático), mais recentes primeiro, inclusive a que estiver aberta.
    /// Contam como Interna enquanto ninguém classificar.
    /// </summary>
    Task<IReadOnlyList<ParadaColetaDto>> ListarPendentesAsync(FiltroParadasPendentes filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca o motivo de uma parada (aberta ou fechada). Grava no histórico o
    /// motivo anterior, o novo e quem trocou; o que o sensor disse continua lá.
    /// O motivo tem que ser da máquina da parada e estar ativo. Mesmo motivo
    /// de antes: nada muda, nada vai para o histórico.
    /// </summary>
    Task<ResultadoCadastro<ParadaColetaDto>> ReclassificarAsync(
        Guid paradaId, Guid motivoId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Histórico de classificação da parada, do mais antigo ao mais novo; nulo se a parada não existe.</summary>
    Task<IReadOnlyList<HistoricoClassificacaoDto>?> HistoricoAsync(Guid paradaId, CancellationToken cancellationToken = default);
}

/// <param name="Limite">Máximo de paradas devolvidas (1 a 1000).</param>
public record FiltroParadasPendentes(
    Guid? MaquinaLinhaId = null,
    Guid? LinhaId = null,
    DateTime? Desde = null,
    DateTime? Ate = null,
    int Limite = 200);

/// <param name="MaquinaId">Máquina do catálogo: os motivos possíveis vêm de <c>GET api/maquinas/{MaquinaId}/motivos-parada</c>.</param>
/// <param name="DuracaoSegundos">Até o fim, ou até agora se ainda está aberta.</param>
/// <param name="Motivo">Nulo = não classificada (conta como Interna).</param>
/// <param name="ClassificadaPeloSistema">Motivo dado por uma regra de sensor (ninguém trocou à mão).</param>
public record ParadaColetaDto(
    Guid Id,
    Guid SessaoId,
    Guid MaquinaLinhaId,
    Guid MaquinaId,
    string Maquina,
    string Linha,
    string Cliente,
    DateTime Inicio,
    DateTime? Fim,
    double DuracaoSegundos,
    Guid? MotivoId,
    string? Motivo,
    TipoParada Tipo,
    bool ClassificadaPeloSistema);

/// <param name="Autor">Nome de quem trocou, ou "Sistema" para a classificação automática.</param>
public record HistoricoClassificacaoDto(
    DateTime AlteradoEm,
    Guid? MotivoAnteriorId,
    string? MotivoAnterior,
    Guid? MotivoNovoId,
    string? MotivoNovo,
    Guid? UsuarioId,
    string Autor);
