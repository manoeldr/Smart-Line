using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Coleta;

/// <summary>Uma entrada do WISE agora, já em texto.</summary>
/// <param name="Recebida">O WISE já mandou esta entrada alguma vez desde que o backend subiu.</param>
/// <param name="Valor">Contadores: último valor bruto.</param>
/// <param name="Incremento">Contadores: quanto subiu desde a mensagem anterior.</param>
/// <param name="ValorBruto">Sensores: como veio (invertidos: true = sem presença).</param>
/// <param name="EmAlarme">Sensores: se está ativo (ex.: faltando garrafa).</param>
/// <param name="Texto">Sensores: o texto de ativo ou de normal que vale agora.</param>
public sealed record EntradaAoVivoDto(
    CanalWise Canal,
    int Entrada,
    TipoCanal Tipo,
    string Nome,
    bool Recebida,
    DateTime? LidoEmUtc,
    uint? Valor,
    long? Incremento,
    double? IntervaloSegundos,
    bool? ValorBruto,
    bool? EmAlarme,
    string? Texto);

/// <summary>Um WISE e suas 8 entradas ao vivo (Validar entradas).</summary>
/// <param name="Conectado">Conexão MQTT aberta agora.</param>
/// <param name="UltimaMensagemUtc">A mais recente conhecida: em memória, ou a anotada na coleta.</param>
/// <param name="Medicao">Coleta em andamento com este WISE; nulo se ele está livre.</param>
public sealed record EntradasDoWiseDto(
    string EnderecoIp,
    bool Conectado,
    DateTime? UltimaMensagemUtc,
    MedicaoDoWiseDto? Medicao,
    IReadOnlyList<EntradaAoVivoDto> Entradas);

/// <summary>Monta o que o Validar entradas mostra. Puro.</summary>
public static class EntradasAoVivo
{
    /// <param name="textos">As 8 entradas com os textos da máquina (ou os padrão).</param>
    public static IReadOnlyList<EntradaAoVivoDto> Montar(IReadOnlyList<EntradaWiseDto> textos, LeituraEntradas? leitura) =>
        textos.Select(t =>
        {
            if (t.Tipo == TipoCanal.Contador)
            {
                var c = leitura?.Contadores.GetValueOrDefault(t.Canal);
                return new EntradaAoVivoDto(t.Canal, t.Entrada, t.Tipo, t.Nome, c is not null, c?.LidoEmUtc,
                    c?.Valor, c?.Incremento, c?.IntervaloSegundos, null, null, null);
            }

            if (leitura is null || !leitura.Estados.TryGetValue(t.Canal, out var bruto))
                return new EntradaAoVivoDto(t.Canal, t.Entrada, t.Tipo, t.Nome, false, null, null, null, null, null, null, null);

            var emAlarme = MapaWise.Definicao(t.Canal).EstaEmAlarme(bruto);
            return new EntradaAoVivoDto(t.Canal, t.Entrada, t.Tipo, t.Nome, true, leitura.UltimaMensagemUtc,
                null, null, null, bruto, emAlarme, emAlarme ? t.TextoAtivo : t.TextoNormal);
        }).ToList();

    /// <summary>Textos padrão, para um WISE livre (sem máquina).</summary>
    public static IReadOnlyList<EntradaWiseDto> TextosPadrao() =>
        MapaWise.Canais.Select(d =>
        {
            var p = TextosEntradasWise.DoPadrao(d.Canal);
            return new EntradaWiseDto(d.Canal, d.Entrada, d.Tipo, d.Funcao, p.Nome, p.TextoAtivo, p.TextoNormal, false);
        }).ToList();
}
