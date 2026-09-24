namespace SmartLine.Core.Entities.Global;

/// <summary>
/// Uma regra: se todas as condições forem verdadeiras, a parada recebe o motivo.
/// O tipo (Interna/Externa) vem do próprio motivo.
/// </summary>
public class RegraClassificacao
{
    public Guid Id { get; set; }
    public Guid ConjuntoRegrasId { get; set; }

    /// <summary>Menor número = avaliada primeiro.</summary>
    public int Prioridade { get; set; }

    /// <summary>Descrição legível para a tela (ex.: "Falta de garrafas na entrada").</summary>
    public string Nome { get; set; } = string.Empty;

    public Guid MotivoParadaId { get; set; }

    /// <summary>Regra desativada é ignorada sem precisar ser apagada.</summary>
    public bool Ativa { get; set; } = true;

    // Navegação
    public ConjuntoRegras ConjuntoRegras { get; set; } = null!;
    public MotivoParada MotivoParada { get; set; } = null!;
    public ICollection<CondicaoRegra> Condicoes { get; set; } = [];
}
