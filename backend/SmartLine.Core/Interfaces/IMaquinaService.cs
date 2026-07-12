namespace SmartLine.Core.Interfaces;

public interface IMaquinaService
{
    Task<IList<MotivoParadaDto>> GetMotivosParadaAsync(Guid maquinaId);
    Task<MotivoParadaDto> CriarMotivoParadaAsync(Guid maquinaId, string nome, string tipo);
    Task<MotivoParadaDto> CriarMotivoParadaPlanejadoAsync(Guid maquinaId, string nome);
}

public record MotivoParadaDto(
    string Id,
    string Nome,
    string Tipo
);