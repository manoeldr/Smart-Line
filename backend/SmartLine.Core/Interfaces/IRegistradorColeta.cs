using SmartLine.Core.Coleta;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Grava no banco os eventos apurados pela coleta automática de um acompanhamento.
/// Comum ao Semi Automático e ao Automático: só conhece <see cref="EventoColeta"/>.
/// </summary>
public interface IRegistradorColeta
{
    /// <summary>
    /// Aplica os eventos em ordem, numa única gravação. Eventos de um
    /// acompanhamento sem sessão em andamento (ex.: finalizado enquanto a
    /// mensagem chegava) são descartados sem erro.
    /// </summary>
    Task RegistrarAsync(Guid acompanhamentoId, IReadOnlyList<EventoColeta> eventos, CancellationToken cancellationToken = default);
}
