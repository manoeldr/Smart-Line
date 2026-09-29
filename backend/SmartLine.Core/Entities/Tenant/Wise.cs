namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// WISE-4051 cadastrado no sistema: a lista de aparelhos que podem ser usados
/// numa medição Semi Automática. Não é de nenhuma máquina: ao iniciar a
/// medição escolhe-se um WISE da lista, que fica associado à máquina só
/// enquanto a medição dura (<see cref="Acompanhamento.EnderecoIpWise"/>).
/// </summary>
public class Wise
{
    public Guid Id { get; set; }

    /// <summary>
    /// IP fixo configurado no WISE (forma canônica, ver <c>EnderecoRede.Normalizar</c>).
    /// É por ele que as mensagens chegam à medição. Não se repete no cadastro.
    /// </summary>
    public string EnderecoIp { get; set; } = string.Empty;

    /// <summary>Opcional, para achar na lista (ex.: "WISE 03", "WISE da maleta").</summary>
    public string? Nome { get; set; }

    public DateTime CriadoEm { get; set; }
}
