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

    /// <summary>Em andamento enquanto não foi finalizado.</summary>
    public bool EmAndamento => FinalizadoEm is null;

    // Navegação
    public MaquinaLinha MaquinaLinha { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario? FinalizadoPor { get; set; }
    public ICollection<AcompanhamentoCanal> Canais { get; set; } = [];
    public ICollection<Sessao> Sessoes { get; set; } = [];
}
