namespace SmartLine.Core.Interfaces;

public interface ISessaoDetalheService
{
    Task<SessaoDetalheDto?> GetUltimaSessaoDetalheAsync(Guid maquinaLinhaId);
}

public record SessaoDetalheDto(
    string SessaoId,
    string MaquinaNome,
    DateTime Inicio,
    DateTime? Fim,
    string Status,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    double? Oee,
    double? Eficiencia,
    double Disponibilidade,
    double Qualidade,
    double TempoRodandoMs,
    double TempoParadoMs,
    int Producao,
    int Refugo,
    double? MttrMs,
    double? MtbfMs,
    IList<CampoGraficoDto> CamposExtras,
    IList<PontoProducaoDto> PontosProducao,
    IList<EventoTimelineDto> Eventos,
    // Máquina do catálogo: de onde vêm os motivos para editar o motivo de uma parada
    string? MaquinaId = null,
    // "Manual" ou "SemiAutomatico": no Semi o gráfico de produção é por hora (ver PorHora)
    string? TipoColeta = null
);

public record CampoGraficoDto(
    string CampoMaquinaId,
    string Nome,
    string? Unidade,
    IList<PontoExtraDto> Pontos
);

public record PontoExtraDto(DateTime Hora, decimal Valor);

/// <param name="Parcial">Semi Automático: hora ainda em andamento (o valor ainda vai crescer).</param>
public record PontoProducaoDto(DateTime Hora, int Quantidade, bool Parcial = false);

public record EventoTimelineDto(
    string Tipo,
    DateTime Horario,
    string? MotivoNome,
    string? MotivoTipo,
    double? DuracaoMs,
    string? FotoPath,
    // Só nas paradas: para editar o motivo e ver o histórico
    string? ParadaId = null,
    string? MotivoId = null
);