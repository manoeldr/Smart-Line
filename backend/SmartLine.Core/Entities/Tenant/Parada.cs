namespace SmartLine.Core.Entities.Tenant;

using SmartLine.Core.Entities.Global;

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
}