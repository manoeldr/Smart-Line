using SmartLine.Core.Interfaces;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;

namespace SmartLine.Tests.Iot;

/// <summary>A lista de WISE da tela Dispositivos IoT e do Configurar medição.</summary>
public class ListaWiseTests
{
    private static readonly DateTime T0 = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static WiseEmMedicao Medicao(string ip, DateTime? ultima = null) =>
        new(ip, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Enchedora", "Linha 1", "Cliente", "Auditor", T0, ultima);

    private static WiseCadastradoDto Cadastro(string ip, string? nome = null) => new(Guid.NewGuid(), ip, nome, T0);

    [Fact]
    public void JuntaCadastradosConectadosVistosEEmMedicao_SemRepetir_OrdenadoPeloIpNumerico()
    {
        var lista = ListaWise.Montar(
            [Cadastro("192.168.10.21", "WISE 21"), Cadastro("192.168.10.30")],
            [new ConexaoMqtt("WISE-21", "192.168.10.21"), new ConexaoMqtt("WISE-9", "192.168.10.9")],
            [new WiseVisto("192.168.10.21", "WISE-21", "t", T0, 5), new WiseVisto("192.168.10.100", "WISE-100", "t", T0, 2)],
            [Medicao("192.168.10.21"), Medicao("192.168.10.50")]);

        Assert.Equal(new[] { "192.168.10.9", "192.168.10.21", "192.168.10.30", "192.168.10.50", "192.168.10.100" }, lista.Select(w => w.EnderecoIp));
        var porIp = lista.ToDictionary(w => w.EnderecoIp);
        Assert.Equal((true, "WISE 21", true, "WISE-21", 5L, true),
            (porIp["192.168.10.21"].Cadastrado, porIp["192.168.10.21"].Nome, porIp["192.168.10.21"].Conectado,
             porIp["192.168.10.21"].ClientId, porIp["192.168.10.21"].Mensagens, porIp["192.168.10.21"].Medicao is not null));
        Assert.Equal((true, false, (string?)null, 0L), (porIp["192.168.10.30"].Cadastrado, porIp["192.168.10.30"].Conectado, porIp["192.168.10.30"].ClientId, porIp["192.168.10.30"].Mensagens));
        Assert.Equal((false, true, (DateTime?)null), (porIp["192.168.10.9"].Cadastrado, porIp["192.168.10.9"].Conectado, porIp["192.168.10.9"].UltimaMensagemUtc));
        Assert.Equal((false, false, "WISE-100"), (porIp["192.168.10.100"].Cadastrado, porIp["192.168.10.100"].Conectado, porIp["192.168.10.100"].ClientId));
    }

    [Fact]
    public void EmMedicaoEDesligado_Aparece_ComAUltimaMensagemAnotadaNaColeta()
    {
        var wise = Assert.Single(ListaWise.Montar([], [], [], [Medicao("192.168.10.50", T0.AddMinutes(-3))]));

        Assert.Equal((false, (string?)null, T0.AddMinutes(-3), "Enchedora"),
            (wise.Conectado, wise.ClientId, wise.UltimaMensagemUtc, wise.Medicao!.Maquina));
    }

    [Fact]
    public void UltimaMensagem_AMaisRecenteEntreMemoriaEColeta()
    {
        var wise = Assert.Single(ListaWise.Montar(
            [], [], [new WiseVisto("192.168.10.21", "W", "t", T0.AddSeconds(30), 1)], [Medicao("192.168.10.21", T0)]));

        Assert.Equal(T0.AddSeconds(30), wise.UltimaMensagemUtc);
        Assert.Equal(T0.AddSeconds(30), ListaWise.MaisRecente(T0, T0.AddSeconds(30)));
        Assert.Equal(T0, ListaWise.MaisRecente(T0, null));
        Assert.Null(ListaWise.MaisRecente(null, null));
    }
}
