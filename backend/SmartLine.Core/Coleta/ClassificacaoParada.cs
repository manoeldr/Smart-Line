using SmartLine.Core.Enums;

namespace SmartLine.Core.Coleta;

/// <summary>
/// Resultado da classificação automática de uma parada.
///
/// Comum aos modos Semi Automático (motor de regras sobre os sensores do WISE)
/// e Automático (estado do PLC). O registrador grava só o
/// <see cref="MotivoParadaId"/> na parada; <see cref="Tipo"/> vem junto para o
/// estado ao vivo e para log, sem precisar ir ao banco.
/// </summary>
/// <param name="Tipo">Tipo do motivo. Interna quando não classificada.</param>
/// <param name="MotivoParadaId">Motivo atribuído. Nulo = não classificada.</param>
/// <param name="RegraId">Regra que decidiu, para auditoria. Nulo quando nenhuma regra bateu.</param>
public sealed record ClassificacaoParada(TipoParada Tipo, Guid? MotivoParadaId, Guid? RegraId)
{
    /// <summary>
    /// Nenhuma regra explicou a parada. Conta como Interna no OEE até alguém
    /// classificar (ver <c>Parada.TipoEfetivo</c>).
    /// </summary>
    public static ClassificacaoParada NaoClassificada { get; } = new(TipoParada.Interna, null, null);

    /// <summary>Verdadeiro quando a parada ficou sem motivo.</summary>
    public bool EhNaoClassificada => MotivoParadaId is null;
}
