namespace SmartLine.Core.Interfaces;

/// <summary>
/// Consultas que o serviço de coleta faz a cada mensagem do WISE: de qual
/// máquina ela é e se essa máquina tem coleta em andamento. Leitura simples,
/// sem rastreamento, para ser barata mesmo com dezenas de WISE publicando.
/// </summary>
public interface ILocalizadorColetaIot
{
    /// <summary>
    /// Máquina da linha em que está o WISE com este IP (forma canônica, a
    /// mesma gravada no cadastro). Nulo se não há WISE ativo com esse IP.
    /// </summary>
    Task<Guid?> MaquinaDoDispositivoAsync(string enderecoIp, CancellationToken cancellationToken = default);

    /// <summary>Acompanhamento em andamento na máquina; nulo se a coleta não está ligada.</summary>
    Task<Guid?> AcompanhamentoEmAndamentoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);
}
