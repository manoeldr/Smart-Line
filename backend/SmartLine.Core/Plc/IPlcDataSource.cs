namespace SmartLine.Core.Plc;

/// <summary>
/// Origem dos dados de uma máquina.
///
/// É o ponto de desacoplamento do Modo Automático: o coletor, o cálculo de OEE
/// e a virada de sessão trabalham só com esta interface e com
/// <see cref="LeituraPlc"/>, sem saber se os dados vieram por S7 (PUT/GET),
/// de um simulador, ou — se um dia fizer sentido — de OPC UA ou WS Protocol.
///
/// Uma instância representa UMA máquina. O coletor mantém uma instância por
/// máquina em coleta, isoladas entre si: PLC fora do ar derruba só a própria.
/// </summary>
/// <remarks>
/// Contrato que toda implementação deve respeitar:
///
/// <para>
/// <b>Conexão preguiçosa.</b> Não conectar no construtor. A primeira chamada a
/// <see cref="LerAsync"/> abre a conexão; as seguintes reaproveitam. Assim dá
/// para registrar todas as máquinas na inicialização sem travar o boot do
/// backend esperando PLC que pode estar desligado.
/// </para>
///
/// <para>
/// <b>Reutilizável.</b> Depois de uma falha, a instância continua válida: a
/// próxima chamada tenta de novo. Quem trata a política de retentativa é o
/// decorator de resiliência, não a implementação base.
/// </para>
///
/// <para>
/// <b>Não é thread-safe.</b> O coletor garante uma chamada por vez para cada
/// máquina. Implementações não precisam de trava interna.
/// </para>
///
/// <para>
/// <b>Timestamp em UTC.</b> A leitura devolvida carrega o instante em que os
/// dados foram lidos, sempre em UTC — hora local aqui desloca o gráfico por
/// hora e a virada de sessão da meia-noite.
/// </para>
/// </remarks>
public interface IPlcDataSource : IAsyncDisposable
{
    /// <summary>
    /// Lê o estado atual da máquina.
    /// </summary>
    /// <remarks>
    /// Deve ser uma leitura única e coerente: todos os campos da
    /// <see cref="LeituraPlc"/> precisam vir do mesmo instante. Ler o contador
    /// numa ida e o status em outra permite creditar produção a um período em
    /// que a máquina já estava parada.
    /// </remarks>
    /// <param name="cancellationToken">Cancelamento (parada do serviço, timeout).</param>
    /// <returns>O retrato da máquina no instante da leitura.</returns>
    /// <exception cref="PlcIndisponivelException">
    /// A máquina não respondeu: conexão recusada, timeout, resposta incompleta.
    /// É a falha esperada e operacional — o coletor trata como período sem
    /// comunicação, não como erro de programação.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// O <paramref name="cancellationToken"/> foi cancelado.
    /// </exception>
    Task<LeituraPlc> LerAsync(CancellationToken cancellationToken = default);
}