namespace SmartLine.Core.Interfaces;

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
