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
    string? TipoColeta = null,
    // Gráficos de paradas (a parada em curso conta até agora)
    IList<ParadaPorHoraDto>? ParadasPorHora = null,
    IList<ParadaPorMotivoDto>? ParadasPorMotivo = null
);

/// <summary>Tempo parado dentro de uma hora (Hora = início da hora), por tipo.</summary>
public record ParadaPorHoraDto(DateTime Hora, double InternaMs, double ExternaMs, double PlanejadaMs);

/// <summary>Tempo parado e número de paradas de um motivo na sessão (maior primeiro).</summary>
/// <param name="MotivoId">Nulo = paradas sem motivo (contam como Interna).</param>
/// <param name="Tipo">Interna, Externa ou Planejada (sem motivo = Interna).</param>
public record ParadaPorMotivoDto(string? MotivoId, string Motivo, string Tipo, double DuracaoMs, int Quantidade);

public record CampoGraficoDto(
    string CampoMaquinaId,
    string Nome,
    string? Unidade,
    IList<PontoExtraDto> Pontos
);

public record PontoExtraDto(DateTime Hora, decimal Valor);

/// <param name="Parcial">Semi Automático: hora ainda em andamento (o valor ainda vai crescer).</param>
/// <param name="SemComunicacao">
/// Semi Automático: produção feita sem comunicação (o contador andou enquanto o WISE estava fora),
/// dividida pelas horas do período. Mostrada à parte (cinza); não entra no OEE.
/// </param>
public record PontoProducaoDto(DateTime Hora, int Quantidade, bool Parcial = false, int SemComunicacao = 0);

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