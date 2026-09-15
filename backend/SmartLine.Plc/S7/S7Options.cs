namespace SmartLine.Plc.S7;

/// <summary>
/// Como conectar no PLC de uma máquina e onde está o DB Weihenstephan.
///
/// Serializada como JSON no cadastro da máquina, então os nomes das
/// propriedades viram o schema gravado no banco: mudar um nome depois exige
/// migrar os registros existentes.
///
/// Cobre só o transporte. Qual dos contadores conta como produção é
/// configuração de coleta, não de conexão, e fica fora daqui.
/// </summary>
public sealed record S7Options
{
    /// <summary>Endereço do PLC na rede industrial.</summary>
    public required string Ip { get; init; }

    /// <summary>
    /// Número do DB que contém o bloco Weihenstephan.
    /// Varia por máquina — na paletizadora usada como base é 15000.
    /// </summary>
    public required int NumeroDb { get; init; }

    /// <summary>Porta ISO-TSAP. 102 em praticamente toda instalação Siemens.</summary>
    public int Porta { get; init; } = 102;

    /// <summary>Rack da CPU. 0 no padrão das linhas S7-1200/1500.</summary>
    public int Rack { get; init; }

    /// <summary>Slot da CPU. 1 para S7-1200/1500; S7-300/400 costuma ser 2.</summary>
    public int Slot { get; init; } = 1;

    /// <summary>Família da CPU. O modelo varia por projeto; S7-1500 é o mais usado hoje.</summary>
    public ModeloCpu Cpu { get; init; } = ModeloCpu.S71500;

    /// <summary>
    /// Timeout para abrir a conexão, em ms.
    /// Curto de propósito: PLC desligado deve falhar rápido e virar período sem
    /// comunicação, não segurar o ciclo do coletor.
    /// </summary>
    public int TimeoutConexaoMs { get; init; } = 5_000;

    /// <summary>Timeout de cada leitura do DB, em ms.</summary>
    public int TimeoutLeituraMs { get; init; } = 5_000;

    /// <summary>
    /// Identificação curta para log e para <c>PlcIndisponivelException.Origem</c>.
    /// Com várias máquinas em coleta, é o que diz qual delas caiu.
    /// </summary>
    public string Descricao => $"{Ip}:{Porta} DB{NumeroDb}";

    /// <summary>
    /// Valida a configuração vinda do JSON.
    /// Roda na hora de montar o data source — erro de cadastro deve aparecer ao
    /// salvar a máquina, não como falha de conexão obscura três dias depois.
    /// </summary>
    /// <exception cref="ArgumentException">Algum campo está inválido.</exception>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Ip))
            throw new ArgumentException("IP do PLC não pode ser vazio.", nameof(Ip));

        if (NumeroDb <= 0)
            throw new ArgumentException(
                $"Número do DB precisa ser positivo (recebido: {NumeroDb}).", nameof(NumeroDb));

        if (Porta is <= 0 or > 65535)
            throw new ArgumentException(
                $"Porta fora da faixa válida (recebido: {Porta}).", nameof(Porta));

        if (Rack < 0)
            throw new ArgumentException(
                $"Rack não pode ser negativo (recebido: {Rack}).", nameof(Rack));

        if (Slot < 0)
            throw new ArgumentException(
                $"Slot não pode ser negativo (recebido: {Slot}).", nameof(Slot));

        if (TimeoutConexaoMs <= 0)
            throw new ArgumentException(
                $"Timeout de conexão precisa ser positivo (recebido: {TimeoutConexaoMs}).",
                nameof(TimeoutConexaoMs));

        if (TimeoutLeituraMs <= 0)
            throw new ArgumentException(
                $"Timeout de leitura precisa ser positivo (recebido: {TimeoutLeituraMs}).",
                nameof(TimeoutLeituraMs));
    }

    /// <summary>
    /// Família da CPU Siemens.
    ///
    /// Enum próprio em vez do tipo da biblioteca de comunicação: este valor é
    /// gravado em JSON no banco, e trocar de biblioteca não pode invalidar o
    /// que já está cadastrado.
    /// </summary>
    public enum ModeloCpu
    {
        /// <summary>S7-1500 — o mais usado nos projetos atuais.</summary>
        S71500 = 0,

        /// <summary>S7-1200.</summary>
        S71200 = 1,

        /// <summary>S7-300 — atenção: slot padrão costuma ser 2, não 1.</summary>
        S7300 = 2,

        /// <summary>S7-400 — atenção: slot padrão costuma ser 2, não 1.</summary>
        S7400 = 3,

        /// <summary>Logo 0BA8.</summary>
        Logo0BA8 = 4
    }
}