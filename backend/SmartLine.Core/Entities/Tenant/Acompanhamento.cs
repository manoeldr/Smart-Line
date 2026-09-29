namespace SmartLine.Core.Entities.Tenant;

/// <summary>
/// Uma coleta automática ligada numa máquina (Semi Automático; no futuro,
/// também Automático): de quem iniciou até quem finalizou.
///
/// Reúne as sessões diárias. A cada meia-noite a sessão do dia é fechada e
/// outra é aberta dentro do mesmo acompanhamento, com o mesmo usuário e a
/// mesma configuração de canais. Só termina com finalização manual.
/// </summary>
public class Acompanhamento
{
    public Guid Id { get; set; }
    public Guid MaquinaLinhaId { get; set; }

    /// <summary>Quem iniciou. Também é o dono das sessões abertas na virada do dia.</summary>
    public Guid UsuarioId { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public Guid? FinalizadoPorId { get; set; }

    /// <summary>
    /// Tempo sem incremento para considerar parada (Z), copiado da máquina da
    /// linha ao iniciar — mesmo padrão da velocidade nominal na sessão, para a
    /// coleta em andamento não mudar se o cadastro mudar.
    /// </summary>
    public int TempoDeteccaoParadaSegundos { get; set; }

    /// <summary>
    /// IP do WISE que mede esta máquina (forma canônica, ver
    /// <c>EnderecoRede.Normalizar</c>), informado ao iniciar. O WISE fica
    /// associado à máquina só enquanto a coleta está em andamento: ao
    /// finalizar ele fica livre para ir para outra máquina. Não há cadastro
    /// de WISE; o histórico de qual WISE mediu cada coleta fica aqui.
    /// Nulo só em coletas sem WISE (reservado para o Automático).
    /// </summary>
    public string? EnderecoIpWise { get; set; }

    /// <summary>
    /// Última mensagem recebida do WISE durante a coleta, anotada no máximo
    /// uma vez por minuto. A retomada usa como evidência de até quando a
    /// máquina estava comunicando.
    /// </summary>
    public DateTime? UltimaMensagemWiseEm { get; set; }

    /// <summary>Em andamento enquanto não foi finalizado.</summary>
    public bool EmAndamento => FinalizadoEm is null;

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario? FinalizadoPor { get; set; }
    public ICollection<AcompanhamentoCanal> Canais { get; set; } = [];
    public ICollection<Sessao> Sessoes { get; set; } = [];
}
