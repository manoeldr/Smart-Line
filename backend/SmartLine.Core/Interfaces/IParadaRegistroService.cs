namespace SmartLine.Core.Interfaces;

public interface IParadaRegistroService
{
    Task<ParadaDto> AbrirAsync(Guid sessaoId, DateTime inicio);
    Task<ParadaDto?> FecharAsync(Guid paradaId, Guid motivoId, DateTime fim);
    Task<ParadaDto?> SalvarFotoAsync(Guid paradaId, Stream conteudoFoto, string extensaoArquivo);
}

public record ParadaDto(
    string Id,
    string SessaoId,
    string? MotivoId,
    DateTime Inicio,
    DateTime? Fim,
    string? FotoPath
);