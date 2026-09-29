using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;

namespace SmartLine.Iot.Coleta;

/// <summary>
/// Um WISE como as telas mostram: se é cadastrado, se está conectado ao
/// broker, o que mandou por último e a medição que o usa (se houver).
/// </summary>
/// <param name="Id">Cadastro do WISE; nulo = não cadastrado (conectou ou publicou, mas não está na lista).</param>
/// <param name="Nome">Nome do cadastro, se houver.</param>
/// <param name="Conectado">Conexão MQTT aberta agora.</param>
/// <param name="ClientId">Identificação MQTT configurada no WISE; nulo se ele não conectou desde que o backend subiu.</param>
/// <param name="UltimaMensagemUtc">A mais recente conhecida: em memória, ou a anotada na coleta.</param>
/// <param name="Mensagens">Mensagens recebidas desde que o backend subiu.</param>
/// <param name="Medicao">Coleta em andamento com este WISE; nulo = livre.</param>
public sealed record WiseDto(
    Guid? Id,
    string EnderecoIp,
    string? Nome,
    bool Conectado,
    string? ClientId,
    DateTime? UltimaMensagemUtc,
    long Mensagens,
    MedicaoDoWiseDto? Medicao)
{
    public bool Cadastrado => Id is not null;
}

/// <summary>A coleta em andamento que está usando um WISE.</summary>
/// <param name="Usuario">Quem iniciou.</param>
public sealed record MedicaoDoWiseDto(
    Guid AcompanhamentoId,
    Guid MaquinaLinhaId,
    string Maquina,
    string Linha,
    string Cliente,
    string Usuario,
    DateTime IniciadoEmUtc)
{
    public static MedicaoDoWiseDto De(WiseEmMedicao w) =>
        new(w.AcompanhamentoId, w.MaquinaLinhaId, w.Maquina, w.Linha, w.Cliente, w.Usuario, w.IniciadoEmUtc);
}

/// <summary>Junta cadastro, broker, motor e banco numa lista só de WISE. Puro.</summary>
public static class ListaWise
{
    /// <summary>
    /// Todo WISE que se sabe existir: cadastrado, conectado ao broker agora,
    /// que publicou desde que o backend subiu, ou que está numa medição em
    /// andamento. Ordenada pelo IP, numericamente.
    /// </summary>
    public static IReadOnlyList<WiseDto> Montar(
        IEnumerable<WiseCadastradoDto> cadastrados,
        IEnumerable<ConexaoMqtt> conexoes,
        IEnumerable<WiseVisto> vistos,
        IEnumerable<WiseEmMedicao> emMedicao)
    {
        var porIpCadastro = cadastrados.GroupBy(c => c.EnderecoIp).ToDictionary(g => g.Key, g => g.First());
        var porIpConexao = conexoes.GroupBy(c => c.EnderecoIp).ToDictionary(g => g.Key, g => g.First());
        var porIpVisto = vistos.ToDictionary(v => v.EnderecoIp);
        var porIpMedicao = emMedicao.GroupBy(m => m.EnderecoIp).ToDictionary(g => g.Key, g => g.First());

        return porIpCadastro.Keys
            .Concat(porIpConexao.Keys)
            .Concat(porIpVisto.Keys)
            .Concat(porIpMedicao.Keys)
            .Where(ip => !string.IsNullOrEmpty(ip))
            .Distinct()
            .Select(ip =>
            {
                var cadastro = porIpCadastro.GetValueOrDefault(ip);
                var conexao = porIpConexao.GetValueOrDefault(ip);
                var visto = porIpVisto.GetValueOrDefault(ip);
                var medicao = porIpMedicao.GetValueOrDefault(ip);
                return new WiseDto(
                    cadastro?.Id,
                    ip,
                    cadastro?.Nome,
                    conexao is not null,
                    conexao?.ClientId ?? visto?.ClientId,
                    MaisRecente(visto?.UltimaMensagemUtc, medicao?.UltimaMensagemEmUtc),
                    visto?.Mensagens ?? 0,
                    medicao is null ? null : MedicaoDoWiseDto.De(medicao));
            })
            .OrderBy(w => w.EnderecoIp, ComparadorIp.Instancia)
            .ToList();
    }

    public static DateTime? MaisRecente(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);
}
