using System.Net;
using MQTTnet;
using MQTTnet.Protocol;

namespace SmartLine.Iot.Simulacao;

/// <summary>
/// Conecta uma <see cref="MaquinaSimulada"/> ao broker como se fosse um WISE:
/// ClientId e tópico próprios e, principalmente, <b>IP de origem próprio</b>
/// (ex.: 127.0.0.21), porque o SmartLine identifica o WISE pelo IP. No mesmo
/// PC, cada máquina simulada sai por um endereço diferente de loopback (toda a
/// faixa 127.x.x.x é do próprio computador, sem configurar nada).
/// </summary>
public sealed class PublicadorWiseSimulado : IDisposable
{
    private readonly MaquinaSimulada _maquina;
    private readonly IPEndPoint _broker;
    private readonly IPAddress _origem;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IMqttClient? _cliente;

    /// <param name="enderecoOrigem">IP pelo qual este "WISE" sai (é o IP a cadastrar no SmartLine).</param>
    /// <param name="enderecoBroker">IP do PC com o SmartLine (ex.: 127.0.0.1 ou 192.168.10.10).</param>
    public PublicadorWiseSimulado(MaquinaSimulada maquina, string enderecoOrigem, string enderecoBroker, int portaBroker)
    {
        _maquina = maquina;
        _origem = IPAddress.Parse(enderecoOrigem);
        _broker = new IPEndPoint(IPAddress.Parse(enderecoBroker), portaBroker);
    }

    public MaquinaSimulada Maquina => _maquina;

    public string EnderecoOrigem => _origem.ToString();

    public string Topico => $"Advantech/SIMULADO{_maquina.Numero:D4}/data";

    public bool Conectado => _cliente?.IsConnected == true;

    /// <summary>
    /// Publica o estado atual. Desligado: derruba a conexão (como um WISE sem
    /// energia) e não publica. Voltando de desligado: reconecta antes.
    /// </summary>
    /// <returns>O conteúdo publicado, ou nulo se não publicou.</returns>
    public async Task<string?> PublicarAsync(DateTime agoraUtc, CancellationToken cancellationToken = default)
    {
        await _trava.WaitAsync(cancellationToken);
        try
        {
            if (!_maquina.Publica)
            {
                Desconectar();
                _maquina.Avancar(agoraUtc);
                return null;
            }

            if (!Conectado)
                await ConectarAsync(cancellationToken);

            var payload = _maquina.GerarPayload(agoraUtc);
            await _cliente!.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(Topico)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(), cancellationToken);
            return payload;
        }
        finally
        {
            _trava.Release();
        }
    }

    public void Dispose()
    {
        Desconectar();
        _trava.Dispose();
    }

    private async Task ConectarAsync(CancellationToken cancellationToken)
    {
        Desconectar();
        var cliente = new MqttClientFactory().CreateMqttClient();
        // WithEndPoint primeiro: é ele que o Build() exige, e ele recria as opções TCP.
        // Depois o WithTcpServer, que mantém o endereço do broker e acrescenta o de origem.
        var opcoes = new MqttClientOptionsBuilder()
            .WithEndPoint(_broker)
            .WithTcpServer(tcp =>
            {
                tcp.RemoteEndpoint = _broker;
                tcp.LocalEndpoint = new IPEndPoint(_origem, 0);
            })
            .WithClientId($"SIM-WISE-{_maquina.Numero}")
            .Build();

        try
        {
            await cliente.ConnectAsync(opcoes, cancellationToken);
            _cliente = cliente;
        }
        catch
        {
            cliente.Dispose();
            throw;
        }
    }

    /// <summary>Fecha a conexão sem despedida, como um WISE que perdeu energia.</summary>
    private void Desconectar()
    {
        _cliente?.Dispose();
        _cliente = null;
    }
}
