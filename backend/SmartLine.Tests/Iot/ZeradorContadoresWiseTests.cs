using System.Net;
using SmartLine.Core.Iot;
using SmartLine.Iot.Rede;
using SmartLine.Iot.Simulacao;

namespace SmartLine.Tests.Iot;

/// <summary>Zerar os contadores do WISE pela interface HTTP, testado contra o WISE simulado.</summary>
public class ZeradorContadoresWiseTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 8, 11, 0, 0, DateTimeKind.Utc);

    private readonly MaquinaSimulada _maquina = new(1, 600, 1, T0);
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly ServidorHttpWise _servidor;

    public ZeradorContadoresWiseTests()
    {
        _maquina.Avancar(T0.AddMinutes(10)); // 6000 pulsos no S2, 60 no S3
        _servidor = new ServidorHttpWise(_maquina, _trava, IPAddress.Loopback, 0);
    }

    public void Dispose()
    {
        _servidor.Dispose();
        _trava.Dispose();
    }

    private ZeradorContadoresWise Zerador(string senha = "00000000") =>
        new(new OpcoesHttpWise { Porta = _servidor.Porta, Senha = senha, Timeout = TimeSpan.FromSeconds(2) });

    [Fact]
    public async Task Zera_OsContadoresMarcados_EOWiseVoltaAContarDoZero()
    {
        Assert.Equal(6000u, _maquina.ContadorProducao);

        using var zerador = Zerador();
        var r = await zerador.ZerarAsync("127.0.0.1", [CanalWise.S2, CanalWise.S3]);

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal((0u, 0u), (_maquina.ContadorProducao, _maquina.ContadorRejeito));
        // O simulador zera S2 e S3 juntos no primeiro PUT; o zerador lê o S3 já em zero e não grava.
        Assert.Equal(1, _servidor.Zeradas);
    }

    [Fact]
    public async Task SenhaErrada_FalhaExplicando_ESemMexerNoContador()
    {
        using var zerador = Zerador(senha: "outra");
        var r = await zerador.ZerarAsync("127.0.0.1", [CanalWise.S2]);

        Assert.False(r.Sucesso);
        Assert.Contains("usuário e a senha", r.Erro);
        Assert.Equal(6000u, _maquina.ContadorProducao);
    }

    [Fact]
    public async Task WiseSemHttp_FalhaSemTravarOInicio()
    {
        // Porta sem ninguém escutando: recusa na hora.
        using var zerador = new ZeradorContadoresWise(new OpcoesHttpWise { Porta = PortaLivre(), Timeout = TimeSpan.FromSeconds(2) });
        var r = await zerador.ZerarAsync("127.0.0.1", [CanalWise.S2]);

        Assert.False(r.Sucesso);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public async Task SemContadores_NaoChamaOWise()
    {
        using var zerador = Zerador();
        Assert.True((await zerador.ZerarAsync("127.0.0.1", [])).Sucesso);
        Assert.Equal(0, _servidor.Zeradas);
    }

    [Fact]
    public void CorpoDoComando_EhOQueATelaDoWiseManda()
    {
        // Capturado da tela do WISE-4051 real em 19/10/2026: PATCH /di_value/slot_0
        Assert.Equal("""{"DIVal":[{"ClrCnt":1,"Ch":1}]}""", ZeradorContadoresWise.CorpoLimparContadores([CanalWise.S2]));
        Assert.Equal("""{"DIVal":[{"ClrCnt":1,"Ch":1},{"ClrCnt":1,"Ch":2}]}""", ZeradorContadoresWise.CorpoLimparContadores([CanalWise.S2, CanalWise.S3]));
        Assert.Equal("/di_value/slot_0", ZeradorContadoresWise.CaminhoSlot);
    }

    [Fact]
    public void Caminho_S1EhCh0_S8EhCh7()
    {
        Assert.Equal("/di_value/slot_0/ch_0", ZeradorContadoresWise.Caminho(CanalWise.S1));
        Assert.Equal("/di_value/slot_0/ch_1", ZeradorContadoresWise.Caminho(CanalWise.S2));
        Assert.Equal("/di_value/slot_0/ch_7", ZeradorContadoresWise.Caminho(CanalWise.S8));
    }

    private static int PortaLivre()
    {
        var ouvinte = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        var porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
        ouvinte.Stop();
        return porta;
    }
}
