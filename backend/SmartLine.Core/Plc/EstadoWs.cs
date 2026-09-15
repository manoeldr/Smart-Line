namespace SmartLine.Core.Plc;

/// <summary>
/// Estados da máquina no padrão Weihenstephan, como implementados no PLC da
/// Sanmartin (tag WS_Cur_State_00300, offset 8 do DB).
///
/// Os valores são <b>bits</b>, não códigos sequenciais: cada estado é uma
/// potência de 2. A lógica do PLC move o valor correspondente à booleana ativa,
/// então na prática costuma vir um bit por vez — mas o formato é de flags, e
/// testar por bit (<c>(codigo &amp; Operating) != 0</c>) funciona tanto para um
/// bit quanto para vários, enquanto comparar por igualdade quebraria em
/// silêncio se a lógica do PLC passasse a combinar estados.
/// </summary>
/// <remarks>
/// A classificação de cada estado para efeito de OEE (rodando, parada interna,
/// parada externa, fora do tempo disponível) NÃO está aqui: depende também do
/// programa em execução, e fica no classificador.
/// </remarks>
[Flags]
public enum EstadoWs : uint
{
    /// <summary>
    /// Nenhuma booleana ativa no PLC.
    /// Acontece no boot, antes da primeira varredura. Não é parada — tratar como
    /// estado indefinido, senão todo start de PLC gera uma parada fantasma.
    /// </summary>
    Nenhum = 0,

    /// <summary>Parada. Se é parada de verdade ou não depende do programa em execução.</summary>
    Stopped = 1,

    /// <summary>Partindo. Transição.</summary>
    Starting = 2,

    /// <summary>Preparada, pronta para operar.</summary>
    Prepared = 4,

    /// <summary>
    /// Falta de produto na entrada. A máquina está pronta, quem não entrega é a
    /// linha a montante — conceito de parada externa.
    /// </summary>
    Lack = 8,

    /// <summary>
    /// Acúmulo na saída. A máquina consegue produzir, mas a linha a jusante não
    /// escoa — parada externa.
    /// </summary>
    Tailback = 16,

    /// <summary>Falta de produto vindo de uma linha ramificada.</summary>
    LackBranchLine = 32,

    /// <summary>Acúmulo em uma linha ramificada.</summary>
    TailbackBranchLine = 64,

    /// <summary>Produzindo. É o estado que conta como tempo rodando.</summary>
    Operating = 128,

    /// <summary>Parando. Transição.</summary>
    Stopping = 256,

    /// <summary>Abortando — parada anormal do ciclo.</summary>
    Aborting = 512,

    /// <summary>Falha da própria máquina. Parada interna: penaliza a Disponibilidade.</summary>
    EquipmentFailure = 1024,

    /// <summary>Falha externa à máquina. Parada externa.</summary>
    ExternalFailure = 2048,

    /// <summary>Emergência acionada.</summary>
    EmergencyStop = 4096,

    // Os bits 8192 e 16384 não são usados na implementação da Sanmartin.
    // Provavelmente estados do padrão que não foram implementados. Se algum dia
    // aparecerem, o classificador precisa tratar bit desconhecido em vez de
    // ignorar em silêncio.

    /// <summary>Ociosa. Sem ordem de produção ativa.</summary>
    Idle = 32768
}