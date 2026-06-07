namespace SmartLine.Core.Interfaces;

public interface IMaquinaService
{
    Task<IList<MotivoParadaDto>> GetMotivosParadaAsync(Guid maquinaId);
}

public record MotivoParadaDto(
    string Id,
    string Nome,
    string Tipo
);
