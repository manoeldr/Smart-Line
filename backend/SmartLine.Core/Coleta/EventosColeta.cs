namespace SmartLine.Core.Coleta;

/// <summary>
/// Fato apurado pela coleta automática de uma máquina.
///
/// É a fronteira entre a interpretação (específica de cada fonte: WISE ou PLC)
/// e o registro no banco (comum). Quem interpreta só emite eventos; quem
/// registra só consome eventos. Assim os dois modos compartilham virada de
/// sessão, consolidação de produção e gravação de paradas.
/// </summary>
/// <remarks>Todos os instantes em UTC.</remarks>
public abstract record EventoColeta(DateTime InstanteUtc);

/// <summary>
/// Produção apurada desde o evento anterior, já convertida em garrafas
/// (pulsos × multiplicador, somando os canais marcados na medição).
/// </summary>
/// <param name="Garrafas">Incremento de produção, em garrafas. Nunca negativo.</param>
/// <param name="Rejeito">Incremento de rejeito, em garrafas. Nunca negativo.</param>
/// <param name="SemComunicacao">
/// Produção feita enquanto a fonte estava sem comunicação: a diferença entre o contador antes
/// da queda e o da volta. Fica gravada à parte e não entra no OEE (não há tempo para ela).
/// </param>
public sealed record ProducaoApurada(DateTime InstanteUtc, long Garrafas, long Rejeito, bool SemComunicacao = false)
    : EventoColeta(InstanteUtc);

/// <summary>
/// A máquina parou. O instante é o do último incremento de produção
/// observado, e não o momento em que a parada foi confirmada.
/// </summary>
public sealed record ParadaIniciada(DateTime InstanteUtc, ClassificacaoParada Classificacao)
    : EventoColeta(InstanteUtc);

/// <summary>
/// A parada que estava sem motivo ganhou uma causa com a máquina ainda parada.
/// O registrador dá o motivo à parada aberta (é a mesma parada, não abre outra),
/// se ninguém a classificou antes.
/// </summary>
public sealed record ParadaReclassificada(DateTime InstanteUtc, ClassificacaoParada Classificacao)
    : EventoColeta(InstanteUtc);

/// <summary>A máquina voltou a produzir, ou a parada foi encerrada por outro motivo.</summary>
public sealed record ParadaEncerrada(DateTime InstanteUtc) : EventoColeta(InstanteUtc);

/// <summary>
/// A fonte parou de responder. O instante é o da última mensagem recebida.
/// Período sem comunicação não é parada da máquina.
/// </summary>
public sealed record ComunicacaoPerdida(DateTime InstanteUtc) : EventoColeta(InstanteUtc);

/// <summary>A fonte voltou a responder.</summary>
/// <param name="ProducaoNaoRecuperada">
/// Um contador voltou do zero durante a queda (WISE reiniciado): a produção do período não
/// tem como ser calculada.
/// </param>
public sealed record ComunicacaoRestabelecida(DateTime InstanteUtc, bool ProducaoNaoRecuperada = false) : EventoColeta(InstanteUtc);

/// <summary>
/// Um contador voltou para trás (dispositivo reiniciou ou foi zerado). O
/// intervalo é descartado, sem produção negativa, e o novo valor vira a
/// referência. Registrado para diagnóstico.
/// </summary>
/// <param name="Contador">Identificação do contador (ex.: "S2").</param>
public sealed record ContadorReiniciado(DateTime InstanteUtc, string Contador, long ValorAnterior, long ValorNovo)
    : EventoColeta(InstanteUtc);
