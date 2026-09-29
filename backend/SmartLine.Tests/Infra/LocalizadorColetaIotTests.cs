using SmartLine.Infrastructure.Coleta;

namespace SmartLine.Tests.Infra;

public class LocalizadorColetaIotTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private async Task<T> Com<T>(Func<LocalizadorColetaIot, Task<T>> acao)
    {
        await using var db = _c.Banco.NovoContexto();
        return await acao(new LocalizadorColetaIot(db));
    }

    [Fact]
    public async Task WiseDaMaquina_EDoIp()
    {
        var porMaquina = await Com(l => l.WiseDaMaquinaAsync(_c.MaquinaLinha));
        var porIp = await Com(l => l.WiseDoIpAsync("192.168.10.21"));

        Assert.Equal(("192.168.10.21", true, _c.MaquinaCatalogo), (porMaquina!.EnderecoIp, porMaquina.Ativo, porMaquina.MaquinaId));
        Assert.Equal(porMaquina, porIp);
        Assert.Null(await Com(l => l.WiseDoIpAsync("192.168.10.99")));
        Assert.Null(await Com(l => l.WiseDaMaquinaAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task MaquinaDoCatalogo()
    {
        Assert.Equal(_c.MaquinaCatalogo, await Com(l => l.MaquinaDoCatalogoAsync(_c.MaquinaLinha)));
        Assert.Null(await Com(l => l.MaquinaDoCatalogoAsync(Guid.NewGuid())));
    }
}
