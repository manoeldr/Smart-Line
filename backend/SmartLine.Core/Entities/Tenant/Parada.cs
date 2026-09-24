namespace SmartLine.Core.Entities.Tenant;

using SmartLine.Core.Entities.Global;
using SmartLine.Core.Enums;

public class Parada
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public Guid? MotivoId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fim { get; set; }

    // Caminho relativo do arquivo de foto (ex: "Sanmartin_RS_Linha1/Sanmartin_RS_Linha1_Enchedora_20260728-143512.jpg").
    // Preparado para a futura funcionalidade de captura de foto na parada — ainda não implementada.
    public string? FotoPath { get; set; }

    // Navegação
    public Sessao Sessao { get; set; } = null!;
    public MotivoParada? Motivo { get; set; }

    /// <summary>
    /// Tipo usado nos cálculos (OEE, MTTR, MTBF, agrupamentos).
    ///
    /// Parada sem motivo é uma parada <b>não classificada</b>: nos modos Semi
    /// Automático e Automático ela nasce assim quando nenhum sensor explica a
    /// causa, e pode ficar pendente até alguém classificar. Enquanto isso conta
    /// como Interna — é a leitura conservadora: penaliza a Disponibilidade em
    /// vez de sumir do cálculo e inflar o OEE.
    /// </summary>
    /// <remarks>
    /// Depende de <see cref="Motivo"/> carregado (<c>Include(p =&gt; p.Motivo)</c>).
    /// Com <see cref="MotivoId"/> preenchido e o motivo não carregado, devolveria
    /// Interna indevidamente. É método, e não propriedade, para o EF não tentar
    /// mapear como coluna.
    /// </remarks>
    public TipoParada TipoEfetivo() => Motivo?.Tipo ?? TipoParada.Interna;
}