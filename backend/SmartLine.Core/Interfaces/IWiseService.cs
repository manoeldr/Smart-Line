namespace SmartLine.Core.Interfaces;

/// <summary>
/// Cadastro dos WISE: a lista de aparelhos que podem ser escolhidos ao iniciar
/// uma medição Semi Automática. O WISE não é de nenhuma máquina.
/// </summary>
public interface IWiseService
{
    /// <summary>Todos os cadastrados, ordenados pelo IP.</summary>
    Task<IReadOnlyList<WiseCadastradoDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Cadastra. Recusa IP inválido ou já cadastrado.</summary>
    Task<ResultadoCadastro<WiseCadastradoDto>> AdicionarAsync(SalvarWiseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Altera IP e nome. Trocar o IP de um WISE em medição é recusado.</summary>
    Task<ResultadoCadastro<WiseCadastradoDto>> EditarAsync(Guid id, SalvarWiseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tira do cadastro. Recusa se o WISE está numa medição em andamento; as
    /// medições já feitas guardam o IP e não são afetadas.
    /// </summary>
    Task<ResultadoCadastro<bool>> RemoverAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <param name="EnderecoIp">Como digitado (ex.: "192.168.10.21").</param>
/// <param name="Nome">Opcional; vazio = sem nome.</param>
public record SalvarWiseRequest(string? EnderecoIp, string? Nome);

public record WiseCadastradoDto(Guid Id, string EnderecoIp, string? Nome, DateTime CriadoEm);

/// <summary>Limites do cadastro de WISE.</summary>
public static class LimitesWise
{
    public const int TamanhoMaximoNome = 60;
}
