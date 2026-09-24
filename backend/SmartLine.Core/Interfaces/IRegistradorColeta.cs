using SmartLine.Core.Coleta;
using SmartLine.Core.Iot;

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

    /// <summary>
    /// Grava a produção acumulada desde a última consolidação como uma nova
    /// leitura (total da sessão em garrafas, como o Manual grava o contador), e
    /// guarda o último valor bruto de cada contador para retomar após reinício.
    /// Sem produção nem rejeito no intervalo, não cria leitura; os valores
    /// brutos são guardados mesmo assim.
    /// </summary>
    /// <param name="contadoresBrutos">Só no Semi Automático; nulo no Automático.</param>
    Task ConsolidarProducaoAsync(
        Guid acompanhamentoId,
        DateTime instanteUtc,
        ProducaoPendente pendente,
        IReadOnlyDictionary<CanalWise, uint>? contadoresBrutos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Virada do dia: consolida o pendente na sessão que termina, fecha-a em
    /// <paramref name="viradaUtc"/> (motivo ViradaDoDia) e abre a do novo dia no
    /// mesmo instante, com o mesmo usuário e a mesma velocidade. Uma parada em
    /// curso é cortada na virada e continua na sessão nova com a mesma
    /// classificação. Idempotente: se a sessão aberta já começa na virada ou
    /// depois, não faz nada.
    /// </summary>
    Task VirarDiaAsync(
        Guid acompanhamentoId,
        DateTime viradaUtc,
        ProducaoPendente pendente,
        IReadOnlyDictionary<CanalWise, uint>? contadoresBrutos,
        CancellationToken cancellationToken = default);
}

/// <summary>Produção apurada e ainda não gravada, em garrafas.</summary>
public readonly record struct ProducaoPendente(long Garrafas, long Rejeito)
{
    public static ProducaoPendente Nenhuma => default;
    public bool Vazia => Garrafas == 0 && Rejeito == 0;
    public ProducaoPendente Somar(ProducaoApurada p) => new(Garrafas + p.Garrafas, Rejeito + p.Rejeito);
}
