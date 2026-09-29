namespace SmartLine.Core.Interfaces;

/// <summary>
/// Consultas que o serviço de coleta faz o tempo todo: de qual máquina é uma
/// mensagem, que coletas estão ligadas, quando o WISE deu sinal pela última vez.
/// Leitura simples, sem rastreamento, para ser barata mesmo com dezenas de
/// WISE publicando.
/// </summary>
/// <remarks>
/// Não há cadastro de WISE: o IP é informado ao iniciar a coleta e fica
/// gravado nela. Um WISE só pertence a uma máquina enquanto a coleta dela
/// está em andamento; fora disso está livre.
/// </remarks>
public interface ILocalizadorColetaIot
{
    /// <summary>
    /// Máquina da linha da coleta em andamento que usa o WISE com este IP
    /// (forma canônica). Nulo se o WISE está livre (nenhuma coleta com ele).
    /// </summary>
    Task<Guid?> MaquinaDoWiseAsync(string enderecoIp, CancellationToken cancellationToken = default);

    /// <summary>Coleta em andamento na máquina; nulo se ela não está ligada.</summary>
    Task<ColetaEmAndamento?> ColetaEmAndamentoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>Todas as coletas em andamento (no máximo uma por máquina).</summary>
    Task<IReadOnlyList<ColetaEmAndamento>> ColetasEmAndamentoAsync(CancellationToken cancellationToken = default);

    /// <summary>Máquina de um acompanhamento (em andamento ou não); nulo se não existe.</summary>
    Task<Guid?> MaquinaDoAcompanhamentoAsync(Guid acompanhamentoId, CancellationToken cancellationToken = default);

    /// <summary>Máquina do catálogo de uma máquina da linha; nulo se a máquina da linha não existe.</summary>
    Task<Guid?> MaquinaDoCatalogoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>WISE em uso agora, um por coleta em andamento, com a máquina que cada um mede.</summary>
    Task<IReadOnlyList<WiseEmMedicao>> WisesEmMedicaoAsync(CancellationToken cancellationToken = default);

    /// <summary>Coleta em andamento que usa o WISE com este IP (forma canônica); nulo se ele está livre.</summary>
    Task<WiseEmMedicao?> WiseEmMedicaoAsync(string enderecoIp, CancellationToken cancellationToken = default);

    /// <summary>Anota na coleta em andamento do WISE o instante da última mensagem recebida. Sem coleta, não faz nada.</summary>
    Task RegistrarUltimaMensagemAsync(string enderecoIp, DateTime instanteUtc, CancellationToken cancellationToken = default);
}

/// <summary>Um WISE medindo uma máquina: a coleta em andamento que o usa.</summary>
/// <param name="MaquinaId">Máquina do catálogo (textos das entradas).</param>
/// <param name="Usuario">Quem iniciou a coleta.</param>
/// <param name="UltimaMensagemEmUtc">Anotada na coleta no máximo uma vez por minuto.</param>
public sealed record WiseEmMedicao(
    string EnderecoIp,
    Guid AcompanhamentoId,
    Guid MaquinaLinhaId,
    Guid MaquinaId,
    string Maquina,
    string Linha,
    string Cliente,
    string Usuario,
    DateTime IniciadoEmUtc,
    DateTime? UltimaMensagemEmUtc);

/// <param name="IniciadoEmUtc">Início da coleta, em UTC.</param>
public sealed record ColetaEmAndamento(Guid AcompanhamentoId, Guid MaquinaLinhaId, DateTime IniciadoEmUtc);
