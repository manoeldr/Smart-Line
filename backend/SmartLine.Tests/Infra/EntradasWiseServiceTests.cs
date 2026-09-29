using SmartLine.Core.Entities.Global;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

public class EntradasWiseServiceTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();
    private readonly Guid _enchedora = Guid.NewGuid();
    private readonly Guid _rotuladora = Guid.NewGuid();

    public EntradasWiseServiceTests()
    {
        using var db = _banco.NovoContexto();
        db.Maquinas.AddRange(
            new Maquina { Id = _enchedora, Nome = "Enchedora", Ativo = true },
            new Maquina { Id = _rotuladora, Nome = "Rotuladora", Ativo = true });
        db.SaveChanges();
    }

    public void Dispose() => _banco.Dispose();

    private async Task<T> Com<T>(Func<EntradasWiseService, Task<T>> acao)
    {
        await using var db = _banco.NovoContexto();
        return await acao(new EntradasWiseService(db));
    }

    private Task<IReadOnlyList<EntradaWiseDto>?> Obter(Guid maquina) => Com(s => s.ObterAsync(maquina));

    private Task<ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>> Salvar(Guid maquina, params SalvarEntradaWiseRequest[] entradas) =>
        Com(s => s.SalvarAsync(maquina, entradas));

    private int Linhas()
    {
        using var db = _banco.NovoContexto();
        return db.TextosEntradasWise.Count();
    }

    [Fact]
    public async Task SemPersonalizacao_AsOitoEntradasComOTextoPadrao()
    {
        var entradas = (await Obter(_enchedora))!;

        Assert.Equal(Enumerable.Range(1, 8).Select(i => (CanalWise)i), entradas.Select(e => e.Canal));
        Assert.All(entradas, e => Assert.False(e.Personalizado));
        var s8 = entradas.Single(e => e.Canal == CanalWise.S8);
        Assert.Equal((7, "Falta de garrafas na entrada", "Com garrafas na entrada"), (s8.Entrada, s8.TextoAtivo, s8.TextoNormal));
        var s2 = entradas.Single(e => e.Canal == CanalWise.S2);
        Assert.Equal((TipoCanal.Contador, (string?)null, (string?)null), (s2.Tipo, s2.TextoAtivo, s2.TextoNormal));
    }

    [Fact]
    public async Task Salvar_PersonalizaSoOQueVeio_ESoNaquelaMaquina()
    {
        var r = await Salvar(_rotuladora,
            new SalvarEntradaWiseRequest(CanalWise.S8, " Garrafas na rotuladora ", " Rotuladora sem garrafa ", "Rotuladora com garrafa"),
            new SalvarEntradaWiseRequest(CanalWise.S2, "Contador de rótulos", "ignorado", "ignorado"));

        var s8 = r.Valor!.Single(e => e.Canal == CanalWise.S8);
        Assert.Equal(("Garrafas na rotuladora", "Rotuladora sem garrafa", "Rotuladora com garrafa", true),
            (s8.Nome, s8.TextoAtivo, s8.TextoNormal, s8.Personalizado));
        var s2 = r.Valor!.Single(e => e.Canal == CanalWise.S2);
        Assert.Equal(("Contador de rótulos", (string?)null, true), (s2.Nome, s2.TextoAtivo, s2.Personalizado));
        Assert.False(r.Valor!.Single(e => e.Canal == CanalWise.S1).Personalizado);
        Assert.All((await Obter(_enchedora))!, e => Assert.False(e.Personalizado));
    }

    [Fact]
    public async Task SalvarIgualAoPadrao_DeixaDeSerPersonalizado()
    {
        await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S8, "Outro", "Ativo", "Normal"));
        Assert.Equal(1, Linhas());

        var padrao = TextosEntradasWise.DoPadrao(CanalWise.S8);
        var r = await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S8, padrao.Nome, padrao.TextoAtivo, padrao.TextoNormal));

        Assert.False(r.Valor!.Single(e => e.Canal == CanalWise.S8).Personalizado);
        Assert.Equal(0, Linhas());
    }

    [Fact]
    public async Task Salvar_Invalido_Recusa_SemGravarNada()
    {
        Assert.Equal("S8: informe o nome.", (await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S8, "  ", "A", "N"))).Erro);
        Assert.Equal("S1: informe o texto de ativo e o de normal.", (await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S1, "Nome", "Ativo", " "))).Erro);
        Assert.Equal("S4 enviada mais de uma vez.", (await Salvar(_enchedora,
            new SalvarEntradaWiseRequest(CanalWise.S4, "A", "B", "C"), new SalvarEntradaWiseRequest(CanalWise.S4, "D", "E", "F"))).Erro);
        Assert.Equal("Entrada inexistente: 9.", (await Salvar(_enchedora, new SalvarEntradaWiseRequest((CanalWise)9, "A"))).Erro);
        Assert.StartsWith("S2: cada texto pode ter até 60", (await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S2, new string('x', 61)))).Erro);
        Assert.Equal(0, Linhas());
    }

    [Fact]
    public async Task RestaurarPadrao_TiraTodaPersonalizacaoDaMaquina()
    {
        await Salvar(_enchedora, new SalvarEntradaWiseRequest(CanalWise.S8, "Outro", "Ativo", "Normal"), new SalvarEntradaWiseRequest(CanalWise.S3, "Refugo"));
        await Salvar(_rotuladora, new SalvarEntradaWiseRequest(CanalWise.S3, "Refugo"));

        var r = await Com(s => s.RestaurarPadraoAsync(_enchedora));

        Assert.All(r!, e => Assert.False(e.Personalizado));
        Assert.Equal(1, Linhas()); // a da Rotuladora continua
    }

    [Fact]
    public async Task MaquinaInexistente()
    {
        Assert.Null(await Obter(Guid.NewGuid()));
        Assert.True((await Salvar(Guid.NewGuid(), new SalvarEntradaWiseRequest(CanalWise.S2, "A"))).NaoEncontrado);
        Assert.Null(await Com(s => s.RestaurarPadraoAsync(Guid.NewGuid())));
    }
}
