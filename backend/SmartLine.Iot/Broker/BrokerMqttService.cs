using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Broker;

/// <summary>
/// Broker MQTT dentro do próprio backend: o WISE publica direto no PC central,
/// sem servidor externo (mantém a filosofia de um único .exe).
///
/// Só recebe e enfileira em <see cref="CaixaDeEntradaMqtt"/>. Não interpreta,
/// não grava, não aplica regra.
/// </summary>
/// <remarks>
/// Se a porta não abrir (outro broker instalado, porta bloqueada), registra o
/// erro e segue: o resto do SmartLine funciona normalmente, só a coleta Semi
/// Automática fica sem dados. Derrubar o app inteiro por causa disso tiraria
/// também o Manual do ar.
/// </remarks>
public sealed class BrokerMqttService : IHostedService, IDisposable
{
    private readonly OpcoesBrokerMqtt _opcoes;
    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly TimeProvider _tempo;
    private readonly ILogger<BrokerMqttService> _log;

    /// <summary>
    /// Chave em que o IP de cada conexão fica guardado na sessão MQTT. A
    /// mensagem publicada não traz o IP de origem; o evento de conexão traz, e
    /// a MQTTnet garante que ele roda antes de qualquer publicação daquela
    /// conexão ser processada, na mesma sessão.
    /// </summary>
    private const string ChaveIpNaSessao = "SmartLine.EnderecoIp";

    /// <summary>Conexões abertas agora: ClientId → IP.</summary>
    private readonly ConcurrentDictionary<string, string> _conexoes = new();

    private MqttServer? _servidor;

    public BrokerMqttService(
        OpcoesBrokerMqtt opcoes,
        CaixaDeEntradaMqtt caixa,
        TimeProvider tempo,
        ILogger<BrokerMqttService> log)
    {
        _opcoes = opcoes;
        _caixa = caixa;
        _tempo = tempo;
        _log = log;
    }

    /// <summary>Broker aberto e aceitando conexões.</summary>
    public bool EmExecucao => _servidor?.IsStarted == true;

    /// <summary>IPs com conexão MQTT aberta agora (tela de dispositivos).</summary>
    public IReadOnlySet<string> IpsConectados() => _conexoes.Values.ToHashSet();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_opcoes.Habilitado)
        {
            _log.LogInformation("Broker MQTT desabilitado (MQTT_HABILITADO=false). Coleta Semi Automática indisponível nesta máquina.");
            return;
        }

        var opcoesServidor = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(_opcoes.Porta)
            .Build();

        var servidor = new MqttServerFactory().CreateMqttServer(opcoesServidor);
        servidor.InterceptingPublishAsync += AoReceberPublicacaoAsync;
        servidor.ClientConnectedAsync += e =>
        {
            var ip = EnderecoRede.Normalizar(e.RemoteEndPoint) ?? string.Empty;
            e.SessionItems[ChaveIpNaSessao] = ip;
            _conexoes[e.ClientId] = ip;
            _log.LogInformation("Dispositivo conectado ao broker: IP {EnderecoIp} (ClientId {ClientId})", ip, e.ClientId);
            return Task.CompletedTask;
        };
        servidor.ClientDisconnectedAsync += e =>
        {
            // Takeover: o mesmo ClientId reconectou e a conexão nova já foi anotada.
            if (e.DisconnectType != MqttClientDisconnectType.Takeover)
                _conexoes.TryRemove(e.ClientId, out _);
            _log.LogWarning("Dispositivo desconectado do broker: {ClientId}", e.ClientId);
            return Task.CompletedTask;
        };

        try
        {
            await servidor.StartAsync();
            _servidor = servidor;
            _log.LogInformation("Broker MQTT escutando na porta {Porta}.", _opcoes.Porta);
        }
        catch (Exception ex)
        {
            servidor.Dispose();
            _log.LogError(ex,
                "Não foi possível abrir o broker MQTT na porta {Porta}. Verifique se outro programa usa a porta " +
                "ou se o firewall bloqueia. O SmartLine segue funcionando; a coleta Semi Automática fica sem dados.",
                _opcoes.Porta);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_servidor is null)
            return;

        await _servidor.StopAsync();
        _log.LogInformation("Broker MQTT encerrado.");
    }

    public void Dispose() => _servidor?.Dispose();

    private Task AoReceberPublicacaoAsync(InterceptingPublishEventArgs e)
    {
        var mensagem = new MensagemMqtt(
            e.ClientId ?? string.Empty,
            e.SessionItems?[ChaveIpNaSessao] as string ?? string.Empty,
            e.ApplicationMessage.Topic ?? string.Empty,
            e.ApplicationMessage.Payload.ToArray(),
            _tempo.GetUtcNow().UtcDateTime);

        _caixa.Publicar(mensagem);

        if (_opcoes.LogBruto)
        {
            _log.LogInformation("[MQTT bruto] {EnderecoIp} | {ClientId} | {Topico} | {Payload}",
                mensagem.EnderecoIp, mensagem.ClientId, mensagem.Topico, mensagem.PayloadComoTexto);
        }

        return Task.CompletedTask;
    }
}
