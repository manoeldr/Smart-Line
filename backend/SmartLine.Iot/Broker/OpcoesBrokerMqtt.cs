namespace SmartLine.Iot.Broker;

/// <summary>
/// Configuração do broker MQTT embutido. Vem das variáveis de ambiente (ou do
/// .env ao lado do .exe), no mesmo padrão do JWT_SECRET e do DATABASE_URL.
/// </summary>
public sealed class OpcoesBrokerMqtt
{
    /// <summary>
    /// MQTT_HABILITADO — "false" ou "0" desliga o broker. Útil nas estações
    /// satélite (modo cliente), que não recebem WISE, e em máquinas que já
    /// tenham outro broker na porta.
    /// </summary>
    public bool Habilitado { get; init; } = true;

    /// <summary>MQTT_PORTA — porta TCP do broker. Padrão MQTT: 1883.</summary>
    public int Porta { get; init; } = 1883;

    /// <summary>
    /// MQTT_LOG_BRUTO — "true" ou "1" escreve no log cada mensagem recebida,
    /// com o conteúdo. É o modo de diagnóstico para capturar o formato real do
    /// WISE na bancada (passo 6). Deixar desligado no uso normal.
    /// </summary>
    public bool LogBruto { get; init; }

    public static OpcoesBrokerMqtt DoAmbiente() => new()
    {
        Habilitado = LerBool("MQTT_HABILITADO") ?? true,
        Porta = int.TryParse(Environment.GetEnvironmentVariable("MQTT_PORTA"), out var porta) && porta is > 0 and <= 65535
            ? porta
            : 1883,
        LogBruto = LerBool("MQTT_LOG_BRUTO") ?? false
    };

    private static bool? LerBool(string variavel) =>
        Environment.GetEnvironmentVariable(variavel)?.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "sim" => true,
            "false" or "0" or "nao" or "não" => false,
            _ => null
        };
}