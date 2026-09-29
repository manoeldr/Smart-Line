using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Coleta;

/// <summary>Situação da conexão de um WISE, como a tela mostra.</summary>
public enum SituacaoConexaoWise
{
    Conectado,
    Desconectado,
    /// <summary>Sem WISE cadastrado, ou cadastrado mas inativo.</summary>
    NaoCadastrado
}

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

/// <summary>WISE de uma máquina (ou de um IP) e suas 8 entradas ao vivo.</summary>
/// <param name="UltimaMensagemUtc">A mais recente conhecida: em memória, ou a anotada no cadastro.</param>
public sealed record SituacaoWiseDto(
    SituacaoConexaoWise Situacao,
    Guid? DispositivoId,
    string? Nome,
    string? EnderecoIp,
    DateTime? UltimaMensagemUtc,
    IReadOnlyList<EntradaAoVivoDto> Entradas);

/// <summary>Monta o que as telas de validação e de início da coleta mostram. Puro.</summary>
public static class EntradasAoVivo
{
    public static SituacaoConexaoWise Situacao(WiseCadastrado? wise, IReadOnlySet<string> ipsConectados) =>
        wise is not { Ativo: true } ? SituacaoConexaoWise.NaoCadastrado
        : ipsConectados.Contains(wise.EnderecoIp) ? SituacaoConexaoWise.Conectado
        : SituacaoConexaoWise.Desconectado;

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

    /// <summary>Textos padrão, para um IP ainda sem máquina.</summary>
    public static IReadOnlyList<EntradaWiseDto> TextosPadrao() =>
        MapaWise.Canais.Select(d =>
        {
            var p = TextosEntradasWise.DoPadrao(d.Canal);
            return new EntradaWiseDto(d.Canal, d.Entrada, d.Tipo, d.Funcao, p.Nome, p.TextoAtivo, p.TextoNormal, false);
        }).ToList();
}
