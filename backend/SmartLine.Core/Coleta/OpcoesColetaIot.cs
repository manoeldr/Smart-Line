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
}
