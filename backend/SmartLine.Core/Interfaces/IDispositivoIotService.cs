namespace SmartLine.Core.Interfaces;

/// <summary>
/// Cadastro dos WISE: qual IP está em qual máquina. É esse vínculo que faz uma
/// mensagem chegar à máquina certa.
/// </summary>
public interface IDispositivoIotService
{
    /// <summary>Todos os WISE, por cliente, linha e ordem da máquina na linha.</summary>
    Task<IReadOnlyList<DispositivoIotDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastra um WISE. O IP é gravado na forma canônica. Recusa IP inválido,
    /// IP já usado por outro WISE e máquina que já tem WISE (um por máquina).
    /// </summary>
    Task<ResultadoCadastro<DispositivoIotDto>> CriarAsync(SalvarDispositivoIotRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Altera um WISE. Com coleta ligada na máquina dele, não deixa trocar de
    /// máquina nem desativar (finalize a coleta antes). Trocar o IP pode: a
    /// próxima mensagem do IP novo já cai na mesma máquina.
    /// </summary>
    Task<ResultadoCadastro<DispositivoIotDto>> EditarAsync(Guid id, SalvarDispositivoIotRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exclui um WISE. Recusa com coleta ligada na máquina dele.</summary>
    Task<ResultadoCadastro<bool>> ExcluirAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <param name="EnderecoIp">IP fixo configurado no WISE, como digitado (ex.: "192.168.10.21").</param>
public record SalvarDispositivoIotRequest(Guid MaquinaLinhaId, string Nome, string EnderecoIp, bool Ativo = true);

/// <param name="ColetaEmAndamento">A máquina do WISE tem coleta Semi Automática ligada.</param>
/// <param name="Conectado">Conexão MQTT aberta agora. Preenchido pela API (vem do broker, não do banco).</param>
public record DispositivoIotDto(
    Guid Id,
    string Nome,
    string EnderecoIp,
    bool Ativo,
    Guid MaquinaLinhaId,
    string Maquina,
    Guid LinhaId,
    string Linha,
    string Cliente,
    DateTime? UltimaMensagemEm,
    bool ColetaEmAndamento,
    bool Conectado = false);

/// <summary>
/// Resultado de uma operação de cadastro: <see cref="Valor"/> no sucesso,
/// <see cref="Erro"/> (mensagem para o usuário) na recusa, ou
/// <see cref="NaoEncontrado"/>.
/// </summary>
public record ResultadoCadastro<T>(T? Valor, string? Erro, bool NaoEncontrado)
{
    public bool Sucesso => Erro is null && !NaoEncontrado;

    public static ResultadoCadastro<T> Ok(T valor) => new(valor, null, false);
    public static ResultadoCadastro<T> Falha(string erro) => new(default, erro, false);
    public static ResultadoCadastro<T> Inexistente() => new(default, null, true);
}
