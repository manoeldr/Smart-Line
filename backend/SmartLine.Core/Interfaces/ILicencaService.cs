namespace SmartLine.Core.Interfaces;

public interface ILicencaService
{
    Task<LicencaStatusDto> ObterStatusAsync();
    Task<bool> AtivarAsync(string chave);
    string ObterMacAddress();
    string GerarChaveEsperada(string macAddress);
}

public record LicencaStatusDto(bool Ativa, string MacAddress);