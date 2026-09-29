using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Coleta;
using static SmartLine.Tests.Infra.CenarioColeta;

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

    private async Task Finalizar()
    {
        await using var db = _c.Banco.NovoContexto();
        Assert.Equal(ResultadoFinalizacao.Finalizado, await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false));
    }

    [Fact]
    public async Task MaquinaDoWise_SoEnquantoAColetaEstaEmAndamento()
    {
        Assert.Equal(_c.MaquinaLinha, await Com(l => l.MaquinaDoWiseAsync(IpWise)));
        Assert.Null(await Com(l => l.MaquinaDoWiseAsync("192.168.10.99")));
        Assert.Null(await Com(l => l.MaquinaDoWiseAsync("")));

        await Finalizar();

        Assert.Null(await Com(l => l.MaquinaDoWiseAsync(IpWise)));
    }

    [Fact]
    public async Task WisesEmMedicao_ComAMaquinaQueCadaUmMede()
    {
        var wise = Assert.Single(await Com(l => l.WisesEmMedicaoAsync()));

        Assert.Equal((IpWise, _c.Acompanhamento, _c.MaquinaLinha, _c.MaquinaCatalogo),
            (wise.EnderecoIp, wise.AcompanhamentoId, wise.MaquinaLinhaId, wise.MaquinaId));
        Assert.Equal(("Enchedora", "Linha 1", "Cliente", "Auditor", T0.UtcDateTime, (DateTime?)null),
            (wise.Maquina, wise.Linha, wise.Cliente, wise.Usuario, wise.IniciadoEmUtc, wise.UltimaMensagemEmUtc));
        Assert.Equal(wise, await Com(l => l.WiseEmMedicaoAsync(IpWise)));
        Assert.Null(await Com(l => l.WiseEmMedicaoAsync("192.168.10.99")));

        await Finalizar();

        Assert.Empty(await Com(l => l.WisesEmMedicaoAsync()));
        Assert.Null(await Com(l => l.WiseEmMedicaoAsync(IpWise)));
    }

    [Fact]
    public async Task UltimaMensagem_AnotadaSoNaColetaEmAndamento()
    {
        await Com(async l => { await l.RegistrarUltimaMensagemAsync(IpWise, Em(30)); return 0; });
        Assert.Equal(Em(30), (await Com(l => l.WiseEmMedicaoAsync(IpWise)))!.UltimaMensagemEmUtc);

        await Finalizar();
        await Com(async l => { await l.RegistrarUltimaMensagemAsync(IpWise, Em(90)); return 0; });

        using var db = _c.Banco.NovoContexto();
        Assert.Equal(Em(30), db.Acompanhamentos.Single().UltimaMensagemWiseEm);
    }

    [Fact]
    public async Task MaquinaDoCatalogo()
    {
        Assert.Equal(_c.MaquinaCatalogo, await Com(l => l.MaquinaDoCatalogoAsync(_c.MaquinaLinha)));
        Assert.Null(await Com(l => l.MaquinaDoCatalogoAsync(Guid.NewGuid())));
    }
}
