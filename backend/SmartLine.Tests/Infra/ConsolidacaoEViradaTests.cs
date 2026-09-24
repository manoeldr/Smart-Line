using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using SmartLine.Core.Services;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

public class ConsolidacaoEViradaTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    // Coleta iniciada às 11:00 UTC (08:00 em Brasília); a virada é às 03:00 UTC do dia seguinte.
    private static readonly DateTime Virada = new(2026, 9, 25, 3, 0, 0, DateTimeKind.Utc);

    // ── Consolidação ────────────────────────────────────────────────

    [Fact]
    public async Task Consolidar_GravaOTotalDaSessao_AcumulandoEntreLeituras()
    {
        await _c.Consolidar(Em(300), garrafas: 1200, rejeito: 5);
        await _c.Consolidar(Em(600), garrafas: 1000, rejeito: 3);

        var leituras = Assert.Single(_c.Sessoes()).Producoes.OrderBy(p => p.Hora).ToList();
        Assert.Equal(3, leituras.Count); // inicial zerada + 2
        Assert.Equal((0, 0), (leituras[0].Quantidade, leituras[0].Refugo));
        Assert.Equal((1200, 5, Em(300)), (leituras[1].Quantidade, leituras[1].Refugo, leituras[1].Hora));
        Assert.Equal((2200, 8, Em(600)), (leituras[2].Quantidade, leituras[2].Refugo, leituras[2].Hora));
    }

    [Fact]
    public async Task Consolidar_SemProducao_NaoCriaLeitura_MasGuardaContadoresBrutos()
    {
        await _c.Consolidar(Em(300), garrafas: 0, brutos: new Dictionary<CanalWise, uint> { [CanalWise.S2] = 4_000_000_000, [CanalWise.S3] = 7 });

        Assert.Single(Assert.Single(_c.Sessoes()).Producoes);
        using var db = _c.Banco.NovoContexto();
        var s2 = db.AcompanhamentoCanais.Single(c => c.Canal == CanalWise.S2);
        Assert.Equal((4_000_000_000L, Em(300)), (s2.UltimoValorBruto!.Value, s2.UltimoValorEm!.Value));
        Assert.Equal(7, db.AcompanhamentoCanais.Single(c => c.Canal == CanalWise.S3).UltimoValorBruto);
        Assert.Null(db.AcompanhamentoCanais.Single(c => c.Canal == CanalWise.S8).UltimoValorBruto);
    }

    [Fact]
    public async Task Consolidar_NoMesmoInstanteDaUltimaLeitura_NaoEmpataAHora()
    {
        await _c.Consolidar(T0.UtcDateTime, garrafas: 50); // mesma hora da leitura inicial

        var leituras = Assert.Single(_c.Sessoes()).Producoes.OrderBy(p => p.Hora).ToList();
        Assert.True(leituras[1].Hora > leituras[0].Hora);
        Assert.Equal(50, leituras[1].Quantidade);
    }

    [Fact]
    public async Task OeeDaSessao_UsaAProducaoConsolidada()
    {
        // 36.000 garrafas/h nominal; 1 h rodando e 18.000 garrafas → Performance 50%.
        await _c.Consolidar(Em(3600), garrafas: 18000, rejeito: 180);
        using (var db = _c.Banco.NovoContexto())
        {
            var s = db.Sessoes.Single();
            s.Fim = Em(3600);
            s.Status = StatusSessao.Finalizada;
            db.SaveChanges();
        }

        var sessao = Assert.Single(_c.Sessoes());
        var oee = new OeeService().Calcular(sessao, sessao.VelocidadeNominal);

        Assert.Equal(50.0, oee.Performance);
        Assert.Equal(99.0, oee.Qualidade);
        Assert.Equal(18000, oee.Producao);
    }

    [Fact]
    public async Task Consolidar_AcompanhamentoFinalizado_NaoFazNada()
    {
        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);

        await _c.Consolidar(Em(300), garrafas: 100);

        Assert.Single(Assert.Single(_c.Sessoes()).Producoes);
    }

    // ── Virada do dia ───────────────────────────────────────────────

    [Fact]
    public async Task Virada_FechaODia_EAbreONovoComOMesmoDonoEVelocidade()
    {
        await _c.Virar(Virada);

        var sessoes = _c.Sessoes();
        Assert.Equal(2, sessoes.Count);
        var (ontem, hoje) = (sessoes[0], sessoes[1]);

        Assert.Equal((StatusSessao.Finalizada, Virada, MotivoFechamentoSessao.ViradaDoDia),
            (ontem.Status, ontem.Fim!.Value, ontem.MotivoFechamento!.Value));

        Assert.Equal(StatusSessao.EmAndamento, hoje.Status);
        Assert.Equal(Virada, hoje.Inicio);
        Assert.Null(hoje.Fim);
        Assert.Equal(_c.Usuario, hoje.UsuarioId);
        Assert.Equal(_c.Acompanhamento, hoje.AcompanhamentoId);
        Assert.Equal(TipoColeta.SemiAutomatico, hoje.TipoColeta);
        Assert.Equal(ontem.VelocidadeNominal, hoje.VelocidadeNominal);
        var inicial = Assert.Single(hoje.Producoes);
        Assert.Equal((0, 0, Virada), (inicial.Quantidade, inicial.Refugo, inicial.Hora));
    }

    [Fact]
    public async Task Virada_ProducaoPendenteFicaNoDiaQueTermina()
    {
        await _c.Consolidar(Em(300), garrafas: 1000);

        await _c.Virar(Virada, garrafas: 250, rejeito: 2);

        var ontem = _c.Sessoes()[0];
        var ultima = ontem.Producoes.OrderBy(p => p.Hora).Last();
        Assert.Equal((1250, 2, Virada), (ultima.Quantidade, ultima.Refugo, ultima.Hora));
    }

    [Fact]
    public async Task Virada_CortaAParadaEmCurso_EContinuaComAMesmaClassificacao()
    {
        var falta = _c.Classificacao("Falta de garrafas na entrada");
        await _c.Registrar(new ParadaIniciada(Virada.AddMinutes(-20), falta));

        await _c.Virar(Virada);

        var (ontem, hoje) = (_c.Sessoes()[0], _c.Sessoes()[1]);
        var antes = Assert.Single(ontem.Paradas);
        Assert.Equal((Virada.AddMinutes(-20), Virada), (antes.Inicio, antes.Fim!.Value));
        var depois = Assert.Single(hoje.Paradas);
        Assert.Equal(Virada, depois.Inicio);
        Assert.Null(depois.Fim);
        Assert.Equal((falta.MotivoParadaId, falta.RegraId), (depois.MotivoId, depois.RegraClassificacaoId));
        var h = Assert.Single(depois.HistoricoClassificacao);
        Assert.Null(h.UsuarioId); // continua sendo classificação do sistema

        // E a próxima ParadaEncerrada fecha o trecho do novo dia.
        await _c.Registrar(new ParadaEncerrada(Virada.AddMinutes(10)));
        Assert.Equal(Virada.AddMinutes(10), _c.Sessoes()[1].Paradas.Single().Fim);
    }

    [Fact]
    public async Task Virada_ParadaClassificadaAMao_ContinuaComOMesmoAutor()
    {
        await _c.Registrar(new ParadaIniciada(Virada.AddMinutes(-20), ClassificacaoParada.NaoClassificada));
        var motivo = _c.Motivo("Falta de garrafas na entrada");
        using (var db = _c.Banco.NovoContexto())
        {
            var p = db.Paradas.Single();
            p.MotivoId = motivo;
            db.HistoricosClassificacaoParada.Add(new HistoricoClassificacaoParada
            {
                Id = Guid.NewGuid(), ParadaId = p.Id, MotivoNovoId = motivo, UsuarioId = _c.Usuario, AlteradoEm = Virada.AddMinutes(-5)
            });
            db.SaveChanges();
        }

        await _c.Virar(Virada);

        var depois = Assert.Single(_c.Sessoes()[1].Paradas);
        Assert.Equal(motivo, depois.MotivoId);
        Assert.Equal(_c.Usuario, Assert.Single(depois.HistoricoClassificacao).UsuarioId);
    }

    [Fact]
    public async Task Virada_ParadaNaoClassificada_ContinuaNaoClassificada()
    {
        await _c.Registrar(new ParadaIniciada(Virada.AddMinutes(-20), ClassificacaoParada.NaoClassificada));

        await _c.Virar(Virada);

        var depois = Assert.Single(_c.Sessoes()[1].Paradas);
        Assert.Null(depois.MotivoId);
        Assert.Empty(depois.HistoricoClassificacao);
    }

    [Fact]
    public async Task Virada_EhIdempotente()
    {
        await _c.Virar(Virada);

        await _c.Virar(Virada);
        await _c.Virar(Virada.AddMinutes(-1)); // atrasada

        Assert.Equal(2, _c.Sessoes().Count);
    }

    [Fact]
    public async Task DuasViradas_TresSessoesEncadeadas()
    {
        await _c.Virar(Virada);
        await _c.Virar(Virada.AddDays(1));

        var sessoes = _c.Sessoes();
        Assert.Equal(3, sessoes.Count);
        Assert.Equal(sessoes[0].Fim, sessoes[1].Inicio);
        Assert.Equal(sessoes[1].Fim, sessoes[2].Inicio);
        Assert.Single(sessoes, s => s.Status == StatusSessao.EmAndamento);
    }

    [Fact]
    public async Task FinalizarDepoisDaVirada_FechaASessaoDoNovoDia()
    {
        await _c.Virar(Virada);
        _c.Tempo.SetUtcNow(new DateTimeOffset(Virada.AddHours(2)));

        await using (var db = _c.Banco.NovoContexto())
            Assert.Equal(Core.Interfaces.ResultadoFinalizacao.Finalizado,
                await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false));

        var sessoes = _c.Sessoes();
        Assert.Equal(MotivoFechamentoSessao.ViradaDoDia, sessoes[0].MotivoFechamento);
        Assert.Equal((MotivoFechamentoSessao.Manual, Virada.AddHours(2)), (sessoes[1].MotivoFechamento!.Value, sessoes[1].Fim!.Value));
    }
}
