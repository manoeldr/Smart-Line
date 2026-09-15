namespace SmartLine.Core.Plc;

/// <summary>
/// A máquina não respondeu: PLC desligado, cabo solto, timeout, rota de rede
/// caída, resposta incompleta.
///
/// É falha <b>operacional</b>, esperada numa fábrica — não é bug. O coletor
/// trata como período sem comunicação (que sai do tempo disponível do OEE, em
/// vez de virar parada Interna e derrubar a Disponibilidade por causa de rede).
///
/// Existir como tipo próprio é o que permite ao coletor capturar só isto, e
/// deixar erro de programação estourar normalmente.
/// </summary>
public sealed class PlcIndisponivelException : Exception
{
    /// <summary>Identificação da máquina ou do endpoint, para log.</summary>
    public string Origem { get; }

    /// <param name="origem">Identificação da máquina ou endpoint (ex.: "192.168.10.21:102 DB50").</param>
    /// <param name="message">O que falhou, em linguagem de log.</param>
    /// <param name="innerException">A exceção original da biblioteca de comunicação, quando houver.</param>
    public PlcIndisponivelException(
        string origem,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Origem = origem;
    }

    /// <inheritdoc />
    public override string ToString() => $"[{Origem}] {base.ToString()}";
}