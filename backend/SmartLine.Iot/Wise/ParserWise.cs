using System.Text.Json;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;

namespace SmartLine.Iot.Wise;

/// <summary>O que o parser concluiu sobre uma mensagem.</summary>
public enum TipoResultadoWise
{
    /// <summary>Mensagem de dados válida; <see cref="ResultadoWise.Amostra"/> preenchida.</summary>
    Dados,

    /// <summary>Tópico que não é de dados (status, controle...). Normal, sem log de erro.</summary>
    Ignorada,

    /// <summary>Tópico de dados, mas conteúdo ilegível. Vale log: pode ser formato diferente do esperado.</summary>
    Invalida
}

/// <param name="Amostra">Só em Dados. O WISE de origem é o IP da mensagem (<see cref="MensagemMqtt.EnderecoIp"/>).</param>
/// <param name="Avisos">Campos descartados (ex.: contador com true/false). Não impedem a amostra.</param>
/// <param name="Motivo">Por que foi ignorada ou é inválida.</param>
public sealed record ResultadoWise(
    TipoResultadoWise Tipo,
    AmostraWise? Amostra,
    IReadOnlyList<string> Avisos,
    string? Motivo);

/// <summary>
/// Converte uma <see cref="MensagemMqtt"/> em <see cref="AmostraWise"/>.
/// Função pura: não lança exceção por conteúdo ruim, devolve
/// <see cref="TipoResultadoWise.Invalida"/> com o motivo. O que é específico do
/// firmware está em <see cref="FormatoWise"/>.
/// </summary>
public static class ParserWise
{
    public static ResultadoWise Interpretar(MensagemMqtt mensagem)
    {
        if (!FormatoWise.EhTopicoDeDados(mensagem.Topico))
            return new(TipoResultadoWise.Ignorada, null, [], $"Tópico não é de dados: {mensagem.Topico}");

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(mensagem.Payload);
        }
        catch (JsonException ex)
        {
            return new(TipoResultadoWise.Invalida, null, [], $"Conteúdo não é JSON: {ex.Message}");
        }

        using (documento)
        {
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
                return new(TipoResultadoWise.Invalida, null, [], "Conteúdo JSON não é um objeto.");

            var contadores = new Dictionary<CanalWise, uint>();
            var estados = new Dictionary<CanalWise, bool>();
            var avisos = new List<string>();

            foreach (var campo in documento.RootElement.EnumerateObject())
            {
                if (FormatoWise.CanalDoCampo(campo.Name) is not { } canal)
                    continue; // s, t, q, c, do1, ai1... não interessam

                if (MapaWise.Definicao(canal).Tipo == TipoCanal.Contador)
                {
                    if (FormatoWise.TentarLerContador(campo.Value, out var contador))
                        contadores[canal] = contador;
                    else
                        avisos.Add($"{campo.Name} ({canal}) é contador, mas veio {campo.Value.GetRawText()}: a entrada está em modo contador no WISE?");
                }
                else
                {
                    if (FormatoWise.TentarLerEstado(campo.Value, out var estado))
                        estados[canal] = estado;
                    else
                        avisos.Add($"{campo.Name} ({canal}) é sensor de estado, mas veio {campo.Value.GetRawText()}.");
                }
            }

            // Sem nenhum canal também é amostra válida: prova que o WISE está vivo
            // (mantém a comunicação) mesmo sem novidade nas entradas.
            var amostra = new AmostraWise
            {
                TimestampUtc = mensagem.RecebidaEmUtc,
                Contadores = contadores,
                Estados = estados
            };
            return new(TipoResultadoWise.Dados, amostra, avisos, null);
        }
    }
}
