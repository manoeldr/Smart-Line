namespace SmartLine.Core.Interfaces;

public interface ILicencaService
{
    Task<LicencaStatusDto> ObterStatusAsync();
    Task<bool> AtivarAsync(string chave);
    /// <summary>MAC preferido para gerar a chave (cabo antes de Wi-Fi), o que a tela de ativação mostra.</summary>
    string ObterMacAddress();

    /// <summary>
    /// MACs de todas as placas físicas do computador, ligadas ou não. A licença vale se a chave
    /// for de qualquer uma delas: tirar o cabo e ficar no Wi-Fi não pede ativação de novo.
    /// </summary>
    IReadOnlyList<string> ObterMacsFisicos();

    string GerarChaveEsperada(string macAddress);
}

public record LicencaStatusDto(bool Ativa, string MacAddress);