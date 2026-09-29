using System.Net;
using System.Net.NetworkInformation;

namespace SmartLine.Iot.Rede;

/// <summary>
/// Teste de ping até um WISE, como o "ping" do Windows: separa problema de
/// rede (não responde) de problema de configuração MQTT (responde, mas não
/// conecta no broker).
/// </summary>
public interface ITestePing
{
    /// <param name="enderecoIp">IP já validado, na forma canônica.</param>
    Task<ResultadoPing> PingarAsync(string enderecoIp, CancellationToken cancellationToken = default);
}

/// <param name="Sequencia">1 a 4, na ordem de envio.</param>
/// <param name="TempoMs">Ida e volta; só quando respondeu.</param>
/// <param name="Situacao">Em texto para a tela (ex.: "Resposta", "Tempo esgotado").</param>
public sealed record RespostaPing(int Sequencia, bool Respondeu, long? TempoMs, string Situacao);

/// <summary>As respostas de um teste de ping e o resumo delas.</summary>
public sealed record ResultadoPing(string EnderecoIp, IReadOnlyList<RespostaPing> Respostas)
{
    public int Enviados => Respostas.Count;
    public int Recebidos => Respostas.Count(r => r.Respondeu);

    /// <summary>Pacotes perdidos, em % (0 a 100).</summary>
    public int PerdaPercentual => Enviados == 0 ? 0 : (int)Math.Round(100.0 * (Enviados - Recebidos) / Enviados);

    public long? TempoMinimoMs => Recebidos > 0 ? Tempos().Min() : null;
    public long? TempoMaximoMs => Recebidos > 0 ? Tempos().Max() : null;
    public long? TempoMedioMs => Recebidos > 0 ? (long)Math.Round(Tempos().Average()) : null;

    private IEnumerable<long> Tempos() => Respostas.Where(r => r.Respondeu).Select(r => r.TempoMs ?? 0);
}

/// <summary>Quatro pings de 32 bytes, 1 s de espera cada, meio segundo entre eles.</summary>
public sealed class TestePing : ITestePing
{
    public const int Tentativas = 4;
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(500);

    public async Task<ResultadoPing> PingarAsync(string enderecoIp, CancellationToken cancellationToken = default)
    {
        var endereco = IPAddress.Parse(enderecoIp);
        var dados = new byte[32];
        var respostas = new List<RespostaPing>(Tentativas);

        using var ping = new Ping();
        for (var sequencia = 1; sequencia <= Tentativas; sequencia++)
        {
            if (sequencia > 1)
                await Task.Delay(Intervalo, cancellationToken);

            try
            {
                var resposta = await ping.SendPingAsync(endereco, Espera, dados, new PingOptions(64, true), cancellationToken);
                respostas.Add(resposta.Status == IPStatus.Success
                    ? new RespostaPing(sequencia, true, resposta.RoundtripTime, "Resposta")
                    : new RespostaPing(sequencia, false, null, Descrever(resposta.Status)));
            }
            catch (PingException ex)
            {
                respostas.Add(new RespostaPing(sequencia, false, null, $"Falha: {(ex.InnerException ?? ex).Message}"));
            }
        }

        return new ResultadoPing(enderecoIp, respostas);
    }

    public static string Descrever(IPStatus status) => status switch
    {
        IPStatus.Success => "Resposta",
        IPStatus.TimedOut => "Tempo esgotado",
        IPStatus.DestinationHostUnreachable => "Host de destino inacessível",
        IPStatus.DestinationNetworkUnreachable => "Rede de destino inacessível",
        IPStatus.DestinationUnreachable => "Destino inacessível",
        IPStatus.TtlExpired or IPStatus.TimeExceeded => "TTL expirado no caminho",
        IPStatus.BadRoute => "Sem rota até o destino",
        _ => status.ToString()
    };
}
