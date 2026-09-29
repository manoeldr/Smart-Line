using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

public class ClassificacaoParadaServiceTests : IDisposable
{
    private const string Falta = "Falta de garrafas na entrada";
    private const string AcumuloMinimo = "Abaixo do acúmulo mínimo";

    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private async Task<T> Com<T>(Func<ClassificacaoParadaService, Task<T>> acao)
    {
        await using var db = _c.Banco.NovoContexto();
        return await acao(new ClassificacaoParadaService(db, _c.Tempo));
    }

    private Task<IReadOnlyList<ParadaColetaDto>> Pendentes(FiltroParadasPendentes? filtro = null) =>
        Com(s => s.ListarPendentesAsync(filtro ?? new FiltroParadasPendentes()));

    private Task<ResultadoCadastro<ParadaColetaDto>> Reclassificar(Guid parada, Guid motivo) =>
        Com(s => s.ReclassificarAsync(parada, motivo, _c.Usuario));

    private void Agora(int segundos) => _c.Tempo.SetUtcNow(new DateTimeOffset(Em(segundos)));

    /// <summary>Uma sem causa fechada (60–120), uma do sensor (200–260) e uma sem causa aberta (desde 300).</summary>
    private async Task TresParadas()
    {
        await _c.Registrar(
            new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada), new ParadaEncerrada(Em(120)),
            new ParadaIniciada(Em(200), _c.Classificacao(Falta)), new ParadaEncerrada(Em(260)),
            new ParadaIniciada(Em(300), ClassificacaoParada.NaoClassificada));
    }

    [Fact]
    public async Task Pendentes_SoAsSemMotivoDaColetaAutomatica_MaisRecentesPrimeiro()
    {
        await TresParadas();
        // Sessão Manual antiga na mesma máquina, com parada sem motivo: não entra.
        using (var db = _c.Banco.NovoContexto())
        {
            var manual = new Sessao
            {
                Id = Guid.NewGuid(), MaquinaLinhaId = _c.MaquinaLinha, UsuarioId = _c.Usuario,
                Inicio = Em(-3600), Fim = Em(-1800), Status = StatusSessao.Finalizada, TipoColeta = TipoColeta.Manual
            };
            db.AddRange(manual, new Parada { Id = Guid.NewGuid(), SessaoId = manual.Id, Inicio = Em(-3000), Fim = Em(-2900) });
            db.SaveChanges();
        }
        Agora(400);

        var lista = await Pendentes();

        Assert.Equal(new[] { (Em(300), (DateTime?)null, 100.0), (Em(60), Em(120), 60.0) },
            lista.Select(p => (p.Inicio, p.Fim, p.DuracaoSegundos)));
        Assert.All(lista, p => Assert.Equal(((string?)null, TipoParada.Interna, "Enchedora", "Linha 1"), (p.Motivo, p.Tipo, p.Maquina, p.Linha)));
        Assert.Equal(_c.MaquinaCatalogo, lista[0].MaquinaId);
    }

    [Fact]
    public async Task Pendentes_Filtros()
    {
        await TresParadas();

        Assert.Empty(await Pendentes(new FiltroParadasPendentes(MaquinaLinhaId: Guid.NewGuid())));
        Assert.Equal(2, (await Pendentes(new FiltroParadasPendentes(MaquinaLinhaId: _c.MaquinaLinha))).Count);
        Assert.Equal(Em(300), Assert.Single(await Pendentes(new FiltroParadasPendentes(Desde: Em(100)))).Inicio);
        Assert.Equal(Em(60), Assert.Single(await Pendentes(new FiltroParadasPendentes(Ate: Em(100)))).Inicio);
        Assert.Single(await Pendentes(new FiltroParadasPendentes(Limite: 1)));

        Guid cliente;
        using (var db = _c.Banco.NovoContexto())
            cliente = db.Clientes.Single().Id;
        Assert.Equal(2, (await Pendentes(new FiltroParadasPendentes(ClienteId: cliente))).Count);
        Assert.Empty(await Pendentes(new FiltroParadasPendentes(ClienteId: Guid.NewGuid())));
    }

    [Fact]
    public async Task Reclassificar_ParadaSemCausa_SaiDasPendentes_EFicaNoHistoricoComQuemFez()
    {
        await TresParadas();
        var semCausa = _c.Paradas()[0].Id;
        Agora(500);

        var r = await Reclassificar(semCausa, _c.Motivo(Falta));

        Assert.Equal((Falta, TipoParada.Externa, false), (r.Valor!.Motivo, r.Valor.Tipo, r.Valor.ClassificadaPeloSistema));
        Assert.DoesNotContain(await Pendentes(), p => p.Id == semCausa);
        var h = Assert.Single((await Com(s => s.HistoricoAsync(semCausa)))!);
        Assert.Equal(((string?)null, Falta, "Auditor", Em(500)), (h.MotivoAnterior, h.MotivoNovo, h.Autor, h.AlteradoEm));
    }

    [Fact]
    public async Task Reclassificar_OQueOSensorClassificou_GuardaAClassificacaoOriginal()
    {
        await TresParadas();
        var doSensor = _c.Paradas()[1];
        Assert.NotNull(doSensor.RegraClassificacaoId);
        Agora(500);

        var r = await Reclassificar(doSensor.Id, _c.Motivo(AcumuloMinimo));

        Assert.Equal((AcumuloMinimo, false), (r.Valor!.Motivo, r.Valor.ClassificadaPeloSistema));
        Assert.Null(_c.Paradas()[1].RegraClassificacaoId);
        var historico = (await Com(s => s.HistoricoAsync(doSensor.Id)))!;
        Assert.Equal(new[] { ((string?)null, (string?)Falta, "Sistema"), (Falta, AcumuloMinimo, "Auditor") },
            historico.Select(h => (h.MotivoAnterior, h.MotivoNovo, h.Autor)));
    }

    [Fact]
    public async Task Reclassificar_ParadaAberta_ContinuaAberta()
    {
        await TresParadas();
        var aberta = _c.Paradas()[2].Id;

        var r = await Reclassificar(aberta, _c.Motivo(Falta));

        Assert.Null(r.Valor!.Fim);
        Assert.Null(_c.Paradas()[2].Fim);
    }

    [Fact]
    public async Task Reclassificar_ParaOMesmoMotivo_NaoMexeNoHistorico()
    {
        await TresParadas();
        var doSensor = _c.Paradas()[1].Id;

        var r = await Reclassificar(doSensor, _c.Motivo(Falta));

        Assert.True(r.Sucesso);
        Assert.Single((await Com(s => s.HistoricoAsync(doSensor)))!);
        Assert.True(r.Valor!.ClassificadaPeloSistema);
    }

    [Fact]
    public async Task Reclassificar_MotivoInvalido_Recusa()
    {
        await TresParadas();
        var parada = _c.Paradas()[0].Id;
        var outraMaquina = new Maquina { Id = Guid.NewGuid(), Nome = "Rotuladora", Ativo = true };
        var deOutraMaquina = new MotivoParada { Id = Guid.NewGuid(), MaquinaId = outraMaquina.Id, Nome = "Rótulo", Tipo = TipoParada.Interna };
        var inativo = new MotivoParada { Id = Guid.NewGuid(), MaquinaId = _c.MaquinaCatalogo, Nome = "Antigo", Tipo = TipoParada.Interna, Ativo = false };
        using (var db = _c.Banco.NovoContexto())
        {
            db.AddRange(outraMaquina, deOutraMaquina, inativo);
            db.SaveChanges();
        }

        Assert.Equal("Motivo não encontrado para esta máquina.", (await Reclassificar(parada, deOutraMaquina.Id)).Erro);
        Assert.Equal("Motivo não encontrado para esta máquina.", (await Reclassificar(parada, Guid.NewGuid())).Erro);
        Assert.Equal("O motivo \"Antigo\" está inativo.", (await Reclassificar(parada, inativo.Id)).Erro);
        Assert.Null(_c.Paradas()[0].MotivoId);
    }

    [Fact]
    public async Task ParadaInexistente()
    {
        Assert.True((await Reclassificar(Guid.NewGuid(), _c.Motivo(Falta))).NaoEncontrado);
        Assert.Null(await Com(s => s.HistoricoAsync(Guid.NewGuid())));
    }
}
