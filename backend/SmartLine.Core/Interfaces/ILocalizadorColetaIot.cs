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

    /// <summary>Máquina do catálogo de uma máquina da linha; nulo se a máquina da linha não existe.</summary>
    Task<Guid?> MaquinaDoCatalogoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>WISE cadastrado na máquina (ativo ou não); nulo se ela não tem.</summary>
    Task<WiseCadastrado?> WiseDaMaquinaAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>WISE cadastrado com este IP (forma canônica, ativo ou não); nulo se não há.</summary>
    Task<WiseCadastrado?> WiseDoIpAsync(string enderecoIp, CancellationToken cancellationToken = default);

    /// <summary>Anota no cadastro do WISE o instante da última mensagem recebida.</summary>
    Task RegistrarUltimaMensagemAsync(string enderecoIp, DateTime instanteUtc, CancellationToken cancellationToken = default);
}

/// <param name="MaquinaId">Máquina do catálogo (textos das entradas).</param>
/// <param name="UltimaMensagemEm">Anotada no cadastro no máximo uma vez por minuto.</param>
public sealed record WiseCadastrado(
    Guid DispositivoId,
    string Nome,
    string EnderecoIp,
    bool Ativo,
    Guid MaquinaLinhaId,
    Guid MaquinaId,
    DateTime? UltimaMensagemEm);

/// <param name="IniciadoEmUtc">Início da coleta, em UTC.</param>
public sealed record ColetaEmAndamento(Guid AcompanhamentoId, Guid MaquinaLinhaId, DateTime IniciadoEmUtc);
