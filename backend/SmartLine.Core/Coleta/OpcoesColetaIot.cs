namespace SmartLine.Core.Coleta;

/// <summary>
/// Parâmetros globais da coleta do Semi Automático (os por máquina ficam no
/// cadastro: Z, canais, multiplicadores, regras).
/// </summary>
public sealed class OpcoesColetaIot
{
    /// <summary>
    /// Sem nenhuma mensagem do WISE por este tempo, a comunicação é dada como
    /// perdida. Proposta da especificação: 3 intervalos de publicação (30 s).
    /// Ajustar quando o intervalo real for definido na bancada.
    /// </summary>
    public TimeSpan TempoSemComunicacao { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>De quanto em quanto tempo a produção acumulada vira uma leitura no banco.</summary>
    public TimeSpan IntervaloConsolidacao { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// De quanto em quanto tempo o relógio da coleta verifica cada máquina (perda
    /// de comunicação, reclassificação por tempo, consolidação, virada do dia).
    /// <see cref="Timeout.InfiniteTimeSpan"/> desliga o relógio automático: os
    /// testes chamam a verificação na mão.
    /// </summary>
    public TimeSpan IntervaloVerificacao { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// De quanto em quanto tempo, no máximo, a última mensagem de cada WISE é
    /// anotada no cadastro. Não precisa ser a cada mensagem: serve para a tela
    /// de dispositivos e como evidência na retomada.
    /// </summary>
    public TimeSpan IntervaloUltimaMensagem { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Fuso em que a sessão vira o dia (meia-noite local).</summary>
    public TimeZoneInfo Fuso { get; init; } = CalendarioColeta.FusoPadrao;
}
