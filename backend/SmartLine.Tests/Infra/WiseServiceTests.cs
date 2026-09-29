using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>
/// Cadastro dos WISE. O cenário já tem o WISE <see cref="CenarioColeta.IpWise"/>
/// cadastrado e numa medição em andamento.
/// </summary>
public class WiseServiceTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private async Task<T> Com<T>(Func<WiseService, Task<T>> acao)
    {
        await using var db = _c.Banco.NovoContexto();
        return await acao(new WiseService(db, _c.Tempo));
    }

    private Task<ResultadoCadastro<WiseCadastradoDto>> Adicionar(string? ip, string? nome = null) =>
        Com(s => s.AdicionarAsync(new SalvarWiseRequest(ip, nome)));

    private async Task<Guid> IdDe(string ip) => (await Com(s => s.ListarAsync())).Single(w => w.EnderecoIp == ip).Id;

    [Fact]
    public async Task Adicionar_GravaIpCanonicoENomeLimpo_EListaOrdenadaPeloIp()
    {
        var r = await Adicionar(" 192.168.10.100 ", "  WISE da maleta  ");
        await Adicionar("192.168.10.9", "   ");

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(("192.168.10.100", "WISE da maleta", T0.UtcDateTime), (r.Valor!.EnderecoIp, r.Valor.Nome, r.Valor.CriadoEm));
        var lista = await Com(s => s.ListarAsync());
        Assert.Equal(new[] { "192.168.10.9", IpWise, "192.168.10.100" }, lista.Select(w => w.EnderecoIp));
        Assert.Null(lista[0].Nome);
    }

    [Theory]
    [InlineData(null, "Informe o IP")]
    [InlineData("", "Informe o IP")]
    [InlineData("192.168.010.021", "IP inválido")]
    [InlineData("wise", "IP inválido")]
    [InlineData(IpWise, "Já existe um WISE cadastrado com o IP 192.168.10.21")]
    public async Task Adicionar_Recusa(string? ip, string erro)
    {
        var r = await Adicionar(ip);

        Assert.False(r.Sucesso);
        Assert.Contains(erro, r.Erro);
    }

    [Fact]
    public async Task Adicionar_NomeLongoDemais_Recusa()
    {
        var r = await Adicionar("192.168.10.40", new string('x', LimitesWise.TamanhoMaximoNome + 1));

        Assert.Contains("no máximo", r.Erro);
    }

    [Fact]
    public async Task Editar_TrocaNomeEIp_DeWiseLivre()
    {
        var id = (await Adicionar("192.168.10.40", "WISE 40")).Valor!.Id;

        var r = await Com(s => s.EditarAsync(id, new SalvarWiseRequest("192.168.10.41", "WISE 41")));

        Assert.True(r.Sucesso, r.Erro);
        Assert.Equal(("192.168.10.41", "WISE 41"), (r.Valor!.EnderecoIp, r.Valor.Nome));
    }

    [Fact]
    public async Task Editar_WiseEmMedicao_SoONome()
    {
        var id = await IdDe(IpWise);

        var nome = await Com(s => s.EditarAsync(id, new SalvarWiseRequest(IpWise, "WISE 21")));
        var ip = await Com(s => s.EditarAsync(id, new SalvarWiseRequest("192.168.10.22", "WISE 21")));

        Assert.True(nome.Sucesso, nome.Erro);
        Assert.False(ip.Sucesso);
        Assert.Contains("está em uso na medição da Enchedora (Linha 1 · Cliente)", ip.Erro);
    }

    [Fact]
    public async Task Editar_ParaIpDeOutro_Recusa_EInexistente()
    {
        var id = (await Adicionar("192.168.10.40")).Valor!.Id;

        var r = await Com(s => s.EditarAsync(id, new SalvarWiseRequest(IpWise, null)));

        Assert.Contains("Já existe um WISE cadastrado com o IP", r.Erro);
        Assert.True((await Com(s => s.EditarAsync(Guid.NewGuid(), new SalvarWiseRequest("192.168.10.50", null)))).NaoEncontrado);
    }

    [Fact]
    public async Task Remover_Livre_Sai_EmMedicao_Recusa_AMedicaoGuardaOIp()
    {
        var livre = (await Adicionar("192.168.10.40")).Valor!.Id;

        var doCenario = await IdDe(IpWise);

        Assert.True((await Com(s => s.RemoverAsync(livre))).Sucesso);
        var emMedicao = await Com(s => s.RemoverAsync(doCenario));
        Assert.Contains("Finalize a medição antes de removê-lo", emMedicao.Erro);

        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);
        Assert.True((await Com(s => s.RemoverAsync(doCenario))).Sucesso);

        using var banco = _c.Banco.NovoContexto();
        Assert.Empty(banco.Wises);
        Assert.Equal(IpWise, banco.Acompanhamentos.Single().EnderecoIpWise);
        Assert.True((await Com(s => s.RemoverAsync(livre))).NaoEncontrado);
    }
}
