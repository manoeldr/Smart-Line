namespace SmartLine.Core.Interfaces;

public interface ILeituraExtraService
{
    Task<LeituraExtraDto> RegistrarAsync(Guid sessaoId, Guid campoMaquinaId, decimal valor, DateTime hora);
}

public record LeituraExtraDto(
    string Id,
    string SessaoId,
    string CampoMaquinaId,
    decimal Valor,
    DateTime Hora
);
