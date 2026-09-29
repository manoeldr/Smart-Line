using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Services;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>O que as telas de configuração da linha e o Overview recebem da coleta automática.</summary>
public class MaquinaLinhaEOverviewTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private Guid LinhaId()
    {
        using var db = _c.Banco.NovoContexto();
        return db.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha).LinhaId;
    }

    private Guid ClienteId()
    {
        using var db = _c.Banco.NovoContexto();
        return db.Clientes.Single().Id;
    }

    private async Task<T> Linhas<T>(Func<LinhaMaquinaService, Task<T>> acao)
    {
        await using var db = _c.Banco.NovoContexto();
        return await acao(new LinhaMaquinaService(db));
    }

    private async Task<MaquinaLinhaOverviewDto> Overview()
    {
        await using var db = _c.Banco.NovoContexto();
        var linhas = await new ClienteService(db, new OeeService()).GetLinhasAsync(ClienteId());
        return linhas.Single().Maquinas.Single();
    }

    // ── Máquina da linha ────────────────────────────────────────────

    [Fact]
    public async Task MaquinaDaLinha_TrazZ_IpDoWise_ESeAsRegrasSaoPersonalizadas()
    {
        var m = (await Linhas(s => s.GetMaquinasDaLinhaAsync(LinhaId()))).Single();

        Assert.Equal((60, "192.168.10.21", false), (m.TempoDeteccaoParadaSegundos, m.EnderecoIpWise, m.RegrasPersonalizadas));

        using (var db = _c.Banco.NovoContexto())
        {
            db.ConjuntosRegras.Add(new ConjuntoRegras { Id = Guid.NewGuid(), MaquinaLinhaId = _c.MaquinaLinha });
            db.DispositivosIot.Single().Ativo = false;
            db.SaveChanges();
        }

        m = (await Linhas(s => s.GetMaquinasDaLinhaAsync(LinhaId()))).Single();
        Assert.Equal(((string?)null, true), (m.EnderecoIpWise, m.RegrasPersonalizadas));
    }

    [Fact]
    public async Task Atualizar_TrocaOZ_SoQuandoInformado()
    {
        var m = await Linhas(s => s.AtualizarAsync(_c.MaquinaLinha, false, 36000, 0, true, tempoDeteccaoParadaSegundos: 120));
        Assert.Equal(120, m!.TempoDeteccaoParadaSegundos);

        m = await Linhas(s => s.AtualizarAsync(_c.MaquinaLinha, true, 36000, 0, true));
        Assert.Equal((120, true), (m!.TempoDeteccaoParadaSegundos, m.Critica));
    }

    [Fact]
    public async Task Adicionar_ZPadraoOuInformado()
    {
        var rotuladora = Guid.NewGuid();
        var encaixotadora = Guid.NewGuid();
        using (var db = _c.Banco.NovoContexto())
        {
            db.Maquinas.AddRange(
                new Maquina { Id = rotuladora, Nome = "Rotuladora", Ativo = true },
                new Maquina { Id = encaixotadora, Nome = "Encaixotadora", Ativo = true });
            db.SaveChanges();
        }

        var padrao = await Linhas(s => s.AdicionarMaquinaAsync(LinhaId(), rotuladora, false, 30000, 0, true));
        var informado = await Linhas(s => s.AdicionarMaquinaAsync(LinhaId(), encaixotadora, false, 30000, 0, true, 45));

        Assert.Equal((60, 45), (padrao.TempoDeteccaoParadaSegundos, informado.TempoDeteccaoParadaSegundos));
        Assert.Equal(("Rotuladora", (string?)null), (padrao.MaquinaNome, padrao.EnderecoIpWise));
    }

    // ── Overview ────────────────────────────────────────────────────

    [Fact]
    public async Task Overview_MarcaAColetaAutomatica_ComWise()
    {
        var m = await Overview();

        Assert.Equal((_c.Acompanhamento.ToString(), "192.168.10.21", 0, (string?)null),
            (m.AcompanhamentoId, m.EnderecoIpWise, m.ParadasSemMotivo, m.MotivoParadaAtual));
        Assert.True(m.SessaoAtiva);
    }

    [Fact]
    public async Task Overview_ContaParadasSemMotivo_EMostraOMotivoDaAtual()
    {
        await _c.Registrar(
            new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada), new ParadaEncerrada(Em(120)),
            new ParadaIniciada(Em(200), _c.Classificacao("Falta de garrafas na entrada")));

        var m = await Overview();

        Assert.Equal((1, "Falta de garrafas na entrada", "ParadaExterna"), (m.ParadasSemMotivo, m.MotivoParadaAtual, m.Status));
    }
}
