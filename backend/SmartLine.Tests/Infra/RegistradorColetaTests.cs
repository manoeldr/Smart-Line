using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

public class RegistradorColetaTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private ClassificacaoParada FaltaGarrafas => _c.Classificacao("Falta de garrafas na entrada");
    private ClassificacaoParada SaidaGarrafas => _c.Classificacao("Acúmulo na saída de garrafas");

    // ── Paradas ─────────────────────────────────────────────────────

    [Fact]
    public async Task ParadaClassificada_GravaMotivoRegra_EHistoricoDoSistema()
    {
        var classificacao = FaltaGarrafas;

        await _c.Registrar(new ParadaIniciada(Em(60), classificacao));

        var p = Assert.Single(_c.Paradas());
        Assert.Equal(Em(60), p.Inicio);
        Assert.Null(p.Fim);
        Assert.Equal(classificacao.MotivoParadaId, p.MotivoId);
        Assert.Equal(classificacao.RegraId, p.RegraClassificacaoId);
        var h = Assert.Single(p.HistoricoClassificacao);
        Assert.Null(h.MotivoAnteriorId);
        Assert.Equal(classificacao.MotivoParadaId, h.MotivoNovoId);
        Assert.Null(h.UsuarioId); // foi o sistema
    }

    [Fact]
    public async Task ParadaNaoClassificada_SemMotivo_ESemHistorico()
    {
        await _c.Registrar(new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada));

        var p = Assert.Single(_c.Paradas());
        Assert.Null(p.MotivoId);
        Assert.Null(p.RegraClassificacaoId);
        Assert.Empty(p.HistoricoClassificacao);
        Assert.Equal(TipoParada.Interna, p.TipoEfetivo());
    }

    [Fact]
    public async Task ParadaEncerrada_FechaAParadaAberta()
    {
        await _c.Registrar(new ParadaIniciada(Em(60), FaltaGarrafas));

        await _c.Registrar(new ParadaEncerrada(Em(300)));

        Assert.Equal(Em(300), Assert.Single(_c.Paradas()).Fim);
    }

    [Fact]
    public async Task Reclassificada_FechaUmTrecho_EAbreOutroNoMesmoInstante()
    {
        var falta = FaltaGarrafas;
        await _c.Registrar(new ParadaIniciada(Em(60), falta));

        await _c.Registrar(new ParadaReclassificada(Em(200), ClassificacaoParada.NaoClassificada));

        var paradas = _c.Paradas();
        Assert.Equal(2, paradas.Count);
        Assert.Equal((Em(60), Em(200), falta.MotivoParadaId), (paradas[0].Inicio, paradas[0].Fim!.Value, paradas[0].MotivoId));
        Assert.Equal(Em(200), paradas[1].Inicio);
        Assert.Null(paradas[1].Fim);
        Assert.Null(paradas[1].MotivoId);
    }

    [Fact]
    public async Task NovaParadaComOutraAberta_FechaAAnterior()
    {
        await _c.Registrar(new ParadaIniciada(Em(60), FaltaGarrafas));

        await _c.Registrar(new ParadaIniciada(Em(500), SaidaGarrafas));

        var paradas = _c.Paradas();
        Assert.Equal(Em(500), paradas[0].Fim);
        Assert.Null(paradas[1].Fim);
    }

    [Fact]
    public async Task EncerradaSemParadaAberta_NaoFazNada()
    {
        await _c.Registrar(new ParadaEncerrada(Em(300)));

        Assert.Empty(_c.Paradas());
    }

    [Fact]
    public async Task InicioAntesDaSessao_ComecaNoInicioDaSessao()
    {
        await _c.Registrar(new ParadaIniciada(Em(-120), FaltaGarrafas));

        var p = Assert.Single(_c.Paradas());
        Assert.Equal(T0.UtcDateTime, p.Inicio);
        Assert.Equal(T0.UtcDateTime, Assert.Single(p.HistoricoClassificacao).AlteradoEm);
    }

    [Fact]
    public async Task VariosEventosNumLote_AplicadosEmOrdem()
    {
        await _c.Registrar(
            new ProducaoApurada(Em(20), 100, 0),
            new ParadaIniciada(Em(60), FaltaGarrafas),
            new ParadaEncerrada(Em(300)),
            new ParadaIniciada(Em(600), ClassificacaoParada.NaoClassificada));

        var paradas = _c.Paradas();
        Assert.Equal(2, paradas.Count);
        Assert.Equal(Em(300), paradas[0].Fim);
        Assert.Null(paradas[1].Fim);
    }

    // ── Comunicação ─────────────────────────────────────────────────

    [Fact]
    public async Task PerdaEVoltaDeComunicacao_GravamUmPeriodo()
    {
        await _c.Registrar(new ComunicacaoPerdida(Em(60)));
        await _c.Registrar(new ComunicacaoPerdida(Em(90))); // repetido: não abre outro

        await _c.Registrar(new ComunicacaoRestabelecida(Em(600)));

        var periodo = Assert.Single(_c.Periodos());
        Assert.Equal((Em(60), Em(600)), (periodo.Inicio, periodo.Fim!.Value));
    }

    [Fact]
    public async Task VoltaECaiDeNovoNoMesmoLote_DoisPeriodos()
    {
        await _c.Registrar(new ComunicacaoPerdida(Em(60)));

        await _c.Registrar(new ComunicacaoRestabelecida(Em(600)), new ComunicacaoPerdida(Em(620)));

        var periodos = _c.Periodos();
        Assert.Equal(2, periodos.Count);
        Assert.Equal(Em(600), periodos[0].Fim);
        Assert.Null(periodos[1].Fim);
    }

    // ── Casos de borda ──────────────────────────────────────────────

    [Fact]
    public async Task AcompanhamentoFinalizado_EventosDescartadosSemErro()
    {
        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);

        await _c.Registrar(new ParadaIniciada(Em(60), FaltaGarrafas), new ComunicacaoPerdida(Em(90)));

        Assert.Empty(_c.Paradas());
        Assert.Empty(_c.Periodos());
    }

    [Fact]
    public async Task SoProducaoOuDiagnostico_NaoGravaNada()
    {
        await _c.Registrar(new ProducaoApurada(Em(20), 100, 2), new ContadorReiniciado(Em(40), "S2", 900, 10));

        Assert.Empty(_c.Paradas());
        Assert.Empty(_c.Periodos());
    }

    // ── Ponta a ponta: amostras → máquina de estados → registrador ─

    [Fact]
    public async Task PontaAPonta_FaltaDeGarrafasQueSeResolveComMaquinaAindaParada()
    {
        var estado = new EstadoMaquinaIot(await _c.Configuracao());
        uint s2 = 1000;

        // Sensores invertidos: S8 = 1 → sem garrafas; S1/S8 normais em 0; S4/S7 normais em 1.
        AmostraWise Amostra(int seg, bool semGarrafa) => new()
        {
            TimestampUtc = Em(seg),
            Contadores = new Dictionary<CanalWise, uint> { [CanalWise.S2] = s2, [CanalWise.S3] = 0 },
            Estados = new Dictionary<CanalWise, bool>
            {
                [CanalWise.S1] = false, [CanalWise.S4] = true, [CanalWise.S7] = true, [CanalWise.S8] = semGarrafa
            }
        };

        async Task Enviar(int seg, bool semGarrafa = false) =>
            await _c.Registrar(estado.Processar(Amostra(seg, semGarrafa)).ToArray());

        for (var s = 0; s <= 60; s += 20) { s2 += 100; await Enviar(s); }        // rodando
        for (var s = 80; s <= 180; s += 20) await Enviar(s, semGarrafa: true);     // parou por falta (confirmada aos 120 s)
        await Enviar(200);                                                         // falta resolvida, segue parada
        await Enviar(220);
        s2 += 100; await Enviar(300);                                              // voltou a produzir

        var paradas = _c.Paradas();
        Assert.Equal(2, paradas.Count);

        var falta = paradas[0];
        Assert.Equal((Em(60), Em(200)), (falta.Inicio, falta.Fim!.Value));
        Assert.Equal("Falta de garrafas na entrada", falta.Motivo!.Nome);
        Assert.Equal(TipoParada.Externa, falta.TipoEfetivo());

        var semCausa = paradas[1];
        Assert.Equal((Em(200), Em(300)), (semCausa.Inicio, semCausa.Fim!.Value));
        Assert.Null(semCausa.MotivoId); // pendente de classificação, conta como Interna
        Assert.Equal(TipoParada.Interna, semCausa.TipoEfetivo());
    }
}
