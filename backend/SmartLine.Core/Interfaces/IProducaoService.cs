namespace SmartLine.Core.Interfaces;

public interface IProducaoService
{
    Task<ProducaoDto> RegistrarAsync(Guid sessaoId, int quantidade, int refugo, DateTime hora);
}

public record ProducaoDto(
    string Id,
    string SessaoId,
    int Quantidade,
    int Refugo,
    DateTime Hora
);