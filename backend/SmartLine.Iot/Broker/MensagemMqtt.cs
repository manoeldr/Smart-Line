using System.Text;
using System.Threading.Channels;

namespace SmartLine.Iot.Broker;

/// <summary>
/// Mensagem publicada por um dispositivo no broker, do jeito que chegou.
/// Nenhuma interpretação aqui: o parser do WISE (passo 4.2) é quem entende o
/// conteúdo.
/// </summary>
/// <param name="ClientId">Identificação da conexão MQTT (configurada no WISE). Só informativo.</param>
/// <param name="EnderecoIp">
/// IP de onde a mensagem veio, na forma canônica (<see cref="EnderecoRede"/>).
/// É ele que identifica o WISE e, pelo cadastro, a máquina. Vazio se o broker
/// não conseguiu determinar (não deveria acontecer em rede TCP).
/// </param>
/// <param name="Topico">Tópico em que o dispositivo publicou.</param>
/// <param name="Payload">Conteúdo bruto.</param>
/// <param name="RecebidaEmUtc">Hora de chegada no backend, em UTC. É o timestamp da amostra.</param>
public sealed record MensagemMqtt(string ClientId, string EnderecoIp, string Topico, byte[] Payload, DateTime RecebidaEmUtc)
{
    /// <summary>Conteúdo como texto (o WISE publica JSON).</summary>
    public string PayloadComoTexto => Encoding.UTF8.GetString(Payload);
}

/// <summary>
/// Fila entre o broker e o serviço de coleta.
///
/// O broker só enfileira e volta a atender os dispositivos: nunca espera banco
/// nem regra. Quem consome é o serviço de coleta (passo 4.4).
/// </summary>
/// <remarks>
/// Limitada a <see cref="Capacidade"/> mensagens. Se o consumo travar e a fila
/// encher, a mais antiga é descartada (e contada em <see cref="Descartadas"/>):
/// melhor perder uma leitura velha do que travar o broker e derrubar a
/// conexão de todos os WISE. Com 10.000 mensagens e publicação a cada 20 s,
/// isso só acontece com o consumo parado por horas.
/// </remarks>
public sealed class CaixaDeEntradaMqtt
{
    public const int Capacidade = 10_000;

    private readonly Channel<MensagemMqtt> _canal;
    private long _descartadas;

    public CaixaDeEntradaMqtt()
    {
        _canal = Channel.CreateBounded<MensagemMqtt>(
            new BoundedChannelOptions(Capacidade)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            },
            _ => Interlocked.Increment(ref _descartadas));
    }

    /// <summary>Lado de leitura, para o serviço de coleta.</summary>
    public ChannelReader<MensagemMqtt> Leitor => _canal.Reader;

    /// <summary>Mensagens perdidas por fila cheia desde que o backend subiu.</summary>
    public long Descartadas => Interlocked.Read(ref _descartadas);

    /// <summary>Enfileira sem esperar. Com a fila cheia, descarta a mais antiga.</summary>
    public void Publicar(MensagemMqtt mensagem) => _canal.Writer.TryWrite(mensagem);
}
