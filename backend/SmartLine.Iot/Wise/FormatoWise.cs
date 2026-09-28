using System.Text.Json;
using System.Text.RegularExpressions;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Wise;

/// <summary>
/// TUDO o que depende do formato de mensagem do firmware do WISE-4051 fica
/// aqui. Se na bancada (MQTT_LOG_BRUTO=true) o formato real vier diferente, é
/// só este arquivo que muda.
/// </summary>
/// <remarks>
/// <para>Formato documentado pela Advantech (confirmado em exemplos publicados):</para>
/// <code>
/// Tópico:   Advantech/00D0C9F941C3/data
/// Conteúdo: {"s":1,"t":"2024-08-08T17:48:21Z","q":192,"c":0,"di1":true,"di2":false}
/// </code>
/// <list type="bullet">
/// <item>O MAC no tópico é ignorado: o WISE é identificado pelo IP de onde a
/// mensagem veio (cadastro do dispositivo).</item>
/// <item><c>diN</c>: entrada digital N (di1 = I0 = S1 ... di8 = I7 = S8).</item>
/// <item><c>s</c>, <c>t</c>, <c>q</c>, <c>c</c>: sequência, hora do WISE, qualidade
/// e contador de mensagens. Ignorados: a hora usada é a de chegada no backend
/// (o relógio do WISE pode estar em 1970 sem SNTP).</item>
/// </list>
/// <para>
/// <b>A confirmar na bancada:</b> como vem uma entrada em modo contador. A
/// suposição é o próprio <c>diN</c> com número (<c>"di2": 12345</c>) em vez de
/// true/false. Enquanto não confirmar, o parser aceita os dois tipos em
/// qualquer canal e decide pelo mapa: contador só aceita número; sensor de
/// estado aceita true/false ou 0/1.
/// </para>
/// </remarks>
public static partial class FormatoWise
{
    /// <summary>Tópico de dados: <c>Advantech/&lt;qualquer coisa&gt;/data</c> (normalmente o MAC).</summary>
    [GeneratedRegex(@"^Advantech/[^/]+/data$")]
    private static partial Regex TopicoDados();

    /// <summary>Campo de entrada digital: <c>di1</c> a <c>di8</c>.</summary>
    [GeneratedRegex(@"^di(?<n>[1-8])$")]
    private static partial Regex CampoEntrada();

    /// <summary>Tópico de dados? Os demais (status, controle...) são ignorados.</summary>
    public static bool EhTopicoDeDados(string topico) => TopicoDados().IsMatch(topico);

    /// <summary>Canal correspondente ao nome do campo JSON, ou nulo se não for uma entrada.</summary>
    public static CanalWise? CanalDoCampo(string nomeCampo)
    {
        var m = CampoEntrada().Match(nomeCampo);
        return m.Success ? (CanalWise)int.Parse(m.Groups["n"].Value) : null;
    }

    /// <summary>Valor de contador: número inteiro entre 0 e 4.294.967.295.</summary>
    public static bool TentarLerContador(JsonElement valor, out uint contador)
    {
        contador = 0;
        return valor.ValueKind == JsonValueKind.Number && valor.TryGetUInt32(out contador);
    }

    /// <summary>Valor bruto de sensor de estado: true/false, ou 0/1.</summary>
    public static bool TentarLerEstado(JsonElement valor, out bool estado)
    {
        switch (valor.ValueKind)
        {
            case JsonValueKind.True:
                estado = true;
                return true;
            case JsonValueKind.False:
                estado = false;
                return true;
            case JsonValueKind.Number when valor.TryGetInt32(out var n) && n is 0 or 1:
                estado = n == 1;
                return true;
            default:
                estado = false;
                return false;
        }
    }
}
