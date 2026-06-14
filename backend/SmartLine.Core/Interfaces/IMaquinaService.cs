namespace SmartLine.Core.Interfaces;

public interface IMaquinaService
{
    Task<IList<MotivoParadaDto>> GetMotivosParadaAsync(Guid maquinaId);
    Task<MotivoParadaDto> CriarMotivoParadaAsync(Guid maquinaId, string nome, string tipo);
}

public record MotivoParadaDto(
    string Id,
    string Nome,
    string Tipo
);