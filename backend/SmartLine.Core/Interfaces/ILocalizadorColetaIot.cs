namespace SmartLine.Core.Interfaces;

/// <summary>
/// Consultas que o serviço de coleta faz o tempo todo: de qual máquina é uma
/// mensagem, que coletas estão ligadas, quando o WISE deu sinal pela última vez.
/// Leitura simples, sem rastreamento, para ser barata mesmo com dezenas de
/// WISE publicando.
/// </summary>
public interface ILocalizadorColetaIot
{
    /// <summary>
    /// Máquina da linha em que está o WISE com este IP (forma canônica, a
    /// mesma gravada no cadastro). Nulo se não há WISE ativo com esse IP.
    /// </summary>
    Task<Guid?> MaquinaDoDispositivoAsync(string enderecoIp, CancellationToken cancellationToken = default);

    /// <summary>Coleta em andamento na máquina; nulo se ela não está ligada.</summary>
    Task<ColetaEmAndamento?> ColetaEmAndamentoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>Todas as coletas em andamento (no máximo uma por máquina).</summary>
    Task<IReadOnlyList<ColetaEmAndamento>> ColetasEmAndamentoAsync(CancellationToken cancellationToken = default);

    /// <summary>Máquina de um acompanhamento (em andamento ou não); nulo se não existe.</summary>
    Task<Guid?> MaquinaDoAcompanhamentoAsync(Guid acompanhamentoId, CancellationToken cancellationToken = default);

    /// <summary>Anota no cadastro do WISE o instante da última mensagem recebida.</summary>
    Task RegistrarUltimaMensagemAsync(string enderecoIp, DateTime instanteUtc, CancellationToken cancellationToken = default);
}

/// <param name="IniciadoEmUtc">Início da coleta, em UTC.</param>
public sealed record ColetaEmAndamento(Guid AcompanhamentoId, Guid MaquinaLinhaId, DateTime IniciadoEmUtc);
