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
    double Oee,
    double Eficiencia,
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
    IList<EventoTimelineDto> Eventos
);

public record CampoGraficoDto(
    string CampoMaquinaId,
    string Nome,
    string? Unidade,
    IList<PontoExtraDto> Pontos
);

public record PontoExtraDto(DateTime Hora, decimal Valor);

public record PontoProducaoDto(DateTime Hora, int Quantidade);

public record EventoTimelineDto(
    string Tipo,
    DateTime Horario,
    string? MotivoNome,
    string? MotivoTipo,
    double? DuracaoMs
);