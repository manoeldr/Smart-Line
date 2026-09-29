using System.Collections.Concurrent;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Coleta;

/// <summary>Último valor de um contador, e quanto subiu desde a mensagem anterior que o trouxe.</summary>
/// <param name="Incremento">Nulo na primeira leitura ou se o contador voltou (WISE reiniciado).</param>
/// <param name="IntervaloSegundos">Tempo entre as duas leituras do incremento.</param>
public sealed record LeituraContador(uint Valor, DateTime LidoEmUtc, long? Incremento, double? IntervaloSegundos);

/// <summary>O que um WISE mostrou por último em cada entrada (para o Validar entradas).</summary>
public sealed record LeituraEntradas(
    string EnderecoIp,
    DateTime UltimaMensagemUtc,
    long Mensagens,
    IReadOnlyDictionary<CanalWise, LeituraContador> Contadores,
    IReadOnlyDictionary<CanalWise, bool> Estados);

/// <summary>
/// Últimas leituras de cada WISE, por IP, cadastrado ou não e com ou sem
/// coleta ligada. Serve para conferir a fiação antes de iniciar: o técnico vê
/// o contador subir e o sensor mudar. Não participa da coleta.
/// </summary>
/// <remarks>
/// Uma mensagem pode trazer só parte das entradas; cada entrada guarda o seu
/// último valor. Thread-safe: escrita pelo despachante, leitura pela API.
/// </remarks>
public sealed class LeiturasEntradasWise
{
    private readonly ConcurrentDictionary<string, LeituraEntradas> _porIp = new();

    public LeituraEntradas? Obter(string enderecoIp) => _porIp.TryGetValue(enderecoIp, out var leitura) ? leitura : null;

    public void Registrar(string enderecoIp, AmostraWise amostra) =>
        _porIp.AddOrUpdate(
            enderecoIp,
            ip => Combinar(ip, null, amostra),
            (ip, anterior) => Combinar(ip, anterior, amostra));

    private static LeituraEntradas Combinar(string ip, LeituraEntradas? anterior, AmostraWise amostra)
    {
        var t = amostra.TimestampUtc;

        var contadores = new Dictionary<CanalWise, LeituraContador>(anterior?.Contadores ?? new Dictionary<CanalWise, LeituraContador>());
        foreach (var (canal, valor) in amostra.Contadores)
        {
            var antes = contadores.GetValueOrDefault(canal);
            contadores[canal] = antes is null || valor < antes.Valor
                ? new LeituraContador(valor, t, null, null)
                : new LeituraContador(valor, t, (long)valor - antes.Valor, (t - antes.LidoEmUtc).TotalSeconds);
        }

        var estados = new Dictionary<CanalWise, bool>(anterior?.Estados ?? new Dictionary<CanalWise, bool>());
        foreach (var (canal, valor) in amostra.Estados)
            estados[canal] = valor;

        return new LeituraEntradas(ip, t, (anterior?.Mensagens ?? 0) + 1, contadores, estados);
    }
}
