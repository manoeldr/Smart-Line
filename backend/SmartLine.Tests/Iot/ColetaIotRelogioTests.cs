using Microsoft.Extensions.Time.Testing;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Tests.Infra;

namespace SmartLine.Tests.Iot;

/// <summary>
/// Relógio da coleta: o que depende do tempo passar (comunicação, consolidação,
/// virada), finalização pelo motor e desligamento. Relógio simulado e ciclos
/// disparados na mão, salvo no teste do relógio automático.
/// </summary>
public class ColetaIotRelogioTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 11, 0, 0, TimeSpan.Zero);
    private static DateTime Em(int segundos) => T0.UtcDateTime.AddSeconds(segundos);
    private const string A = AmbienteColetaIot.IpA;

    private readonly FakeTimeProvider _tempo = new(T0);
    private AmbienteColetaIot _amb = null!;
    private long _enviadas;

    public async Task InitializeAsync()
    {
        _amb = new AmbienteColetaIot(_tempo);
        await _amb.SubirAsync();
    }

    public async Task DisposeAsync() => await _amb.DisposeAsync();

    private void Enviar(DateTime quando, uint s2, uint s3 = 0)
    {
        _amb.Enviar(A, quando, s2, s3);
        _enviadas++;
    }

    private void Enviar(int segundos, uint s2, uint s3 = 0) => Enviar(Em(segundos), s2, s3);

    private Task Aguardar() => _amb.AguardarMensagensAsync(_enviadas);

    private async Task Relogio(DateTime agora)
    {
        _tempo.SetUtcNow(new DateTimeOffset(agora));
        await _amb.Servico.VerificarAgoraAsync();
    }

    private SituacaoColetaIotResumo Situacao()
    {
        var s = _amb.Servico.Situacao(_amb.MaquinaA);
        return s is null ? default : new(s.Situacao, s.ProducaoPendente);
    }

    private readonly record struct SituacaoColetaIotResumo(SituacaoMaquina? Situacao, ProducaoPendente Pendente);

    // ── Comunicação ─────────────────────────────────────────────────

    [Fact]
    public async Task WiseCalado_RelogioRegistraSemComunicacaoDesdeAUltimaMensagem()
    {
        await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(0, s2: 0);
        Enviar(20, s2: 100);
        await Aguardar();

        await Relogio(Em(110)); // 90 s exatos: ainda não
        Assert.Empty(_amb.Periodos(_amb.MaquinaA));

        await Relogio(Em(111));
        var periodo = Assert.Single(_amb.Periodos(_amb.MaquinaA));
        Assert.Equal(Em(20), periodo.Inicio);
        Assert.Null(periodo.Fim);
        Assert.Equal(SituacaoMaquina.SemComunicacao, Situacao().Situacao);
    }

    [Fact]
    public async Task ColetaSemNenhumaMensagem_FicaSemComunicacaoDesdeOInicio()
    {
        await _amb.IniciarColetaAsync(_amb.MaquinaA);

        await Relogio(Em(60));
        Assert.Equal(SituacaoMaquina.AguardandoPrimeiraAmostra, Situacao().Situacao); // o relógio já conhece a coleta
        Assert.Empty(_amb.Periodos(_amb.MaquinaA));

        await Relogio(Em(100));
        Assert.Equal(SituacaoMaquina.SemComunicacao, Situacao().Situacao);
        Assert.Equal(Em(0), Assert.Single(_amb.Periodos(_amb.MaquinaA)).Inicio);

        Enviar(120, s2: 500); // WISE finalmente aparece
        await Aguardar();

        Assert.Equal(Em(120), Assert.Single(_amb.Periodos(_amb.MaquinaA)).Fim);
        Assert.Equal(new SituacaoColetaIotResumo(SituacaoMaquina.Rodando, ProducaoPendente.Nenhuma), Situacao());
    }

    // ── Consolidação ────────────────────────────────────────────────

    [Fact]
    public async Task ACadaCincoMinutos_GravaAProducaoEOsContadores_ENadaSeOWiseCalou()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(0, s2: 1000, s3: 10);
        Enviar(20, s2: 1600, s3: 16);
        await Aguardar();

        await Relogio(Em(299));
        Assert.Single(Assert.Single(_amb.Sessoes(acompanhamento)).Producoes); // só a inicial

        await Relogio(Em(300)); // 11:05
        var leitura = AmbienteColetaIot.UltimaLeitura(Assert.Single(_amb.Sessoes(acompanhamento)));
        Assert.Equal((600, 6, Em(300)), (leitura.Quantidade, leitura.Refugo, leitura.Hora));
        Assert.Equal(ProducaoPendente.Nenhuma, Situacao().Pendente);
        _amb.Alterar(db => Assert.Equal(1600, db.AcompanhamentoCanais.Single(c => c.AcompanhamentoId == acompanhamento && c.Canal == CanalWise.S2).UltimoValorBruto));

        await Relogio(Em(600)); // 11:10, WISE calado desde 11:00:20
        Assert.Equal(2, Assert.Single(_amb.Sessoes(acompanhamento)).Producoes.Count);
        _amb.Alterar(db => Assert.Equal(Em(300), db.AcompanhamentoCanais.Single(c => c.AcompanhamentoId == acompanhamento && c.Canal == CanalWise.S2).UltimoValorEm));
    }

    // ── Virada do dia ───────────────────────────────────────────────

    [Fact]
    public async Task MeiaNoite_FechaASessaoComAProducaoDoDia_ESegueNaNova()
    {
        // 02:55 UTC = 23:55 em Brasília; virada às 03:00 UTC.
        var inicio = new DateTime(2026, 9, 25, 2, 55, 0, DateTimeKind.Utc);
        var virada = new DateTime(2026, 9, 25, 3, 0, 0, DateTimeKind.Utc);
        _tempo.SetUtcNow(new DateTimeOffset(inicio));
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);

        // A cada 20 s, 100 garrafas, de 23:55:00 a 00:00:20. Sem relógio: a virada
        // vem da própria mensagem de depois da meia-noite.
        for (var i = 0; i <= 16; i++)
            Enviar(inicio.AddSeconds(i * 20), s2: (uint)(i * 100));
        await Aguardar();

        var sessoes = _amb.Sessoes(acompanhamento);
        Assert.Equal(2, sessoes.Count);
        Assert.Equal((StatusSessao.Finalizada, virada, MotivoFechamentoSessao.ViradaDoDia),
            (sessoes[0].Status, sessoes[0].Fim!.Value, sessoes[0].MotivoFechamento!.Value));
        Assert.Equal(1400, AmbienteColetaIot.UltimaLeitura(sessoes[0]).Quantidade); // até 23:59:40
        Assert.Equal((StatusSessao.EmAndamento, virada), (sessoes[1].Status, sessoes[1].Inicio));
        Assert.Equal(200, Situacao().Pendente.Garrafas);                           // 00:00:00 e 00:00:20
    }

    // ── Finalizar e desligar ────────────────────────────────────────

    [Fact]
    public async Task FinalizarPeloMotor_GravaAProducaoPendenteAntesDeEncerrar()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(0, s2: 1000);
        Enviar(20, s2: 1300, s3: 3);
        await Aguardar();
        _tempo.SetUtcNow(new DateTimeOffset(Em(30)));

        var resultado = await _amb.Servico.FinalizarAsync(acompanhamento, _amb.Usuario, podeFinalizarDeOutros: false);

        Assert.Equal(ResultadoFinalizacao.Finalizado, resultado);
        var sessao = Assert.Single(_amb.Sessoes(acompanhamento));
        Assert.Equal((StatusSessao.Finalizada, Em(30)), (sessao.Status, sessao.Fim!.Value));
        Assert.Equal((300, 3), (AmbienteColetaIot.UltimaLeitura(sessao).Quantidade, AmbienteColetaIot.UltimaLeitura(sessao).Refugo));
        Assert.Null(_amb.Servico.Situacao(_amb.MaquinaA));
    }

    [Fact]
    public async Task FinalizarSemPermissao_NaoEncerra_EAColetaSegue()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(0, s2: 0);
        await Aguardar();

        var resultado = await _amb.Servico.FinalizarAsync(acompanhamento, Guid.NewGuid(), podeFinalizarDeOutros: false);

        Assert.Equal(ResultadoFinalizacao.SemPermissao, resultado);
        Assert.Equal(StatusSessao.EmAndamento, Assert.Single(_amb.Sessoes(acompanhamento)).Status);
        Assert.NotNull(_amb.Servico.Situacao(_amb.MaquinaA));
        Assert.Equal(ResultadoFinalizacao.NaoEncontrado, await _amb.Servico.FinalizarAsync(Guid.NewGuid(), _amb.Usuario, true));
    }

    [Fact]
    public async Task DesligarOBackend_GravaAProducaoPendente()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        Enviar(0, s2: 1000);
        Enviar(20, s2: 1250);
        await Aguardar();
        _tempo.SetUtcNow(new DateTimeOffset(Em(30)));

        await _amb.PararAsync();

        var sessao = Assert.Single(_amb.Sessoes(acompanhamento));
        Assert.Equal(StatusSessao.EmAndamento, sessao.Status); // desligar não finaliza: a retomada continua
        Assert.Equal((250, Em(30)), (AmbienteColetaIot.UltimaLeitura(sessao).Quantidade, AmbienteColetaIot.UltimaLeitura(sessao).Hora));
    }

    // ── Última mensagem do WISE ─────────────────────────────────────

    [Fact]
    public async Task UltimaMensagemDoWise_AnotadaNaColeta_NoMaximoUmaVezPorMinuto()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);

        Enviar(0, s2: 1);
        await Aguardar();
        Assert.Equal(Em(0), _amb.UltimaMensagemDoWise(acompanhamento));

        Enviar(20, s2: 2);
        await Aguardar();
        Assert.Equal(Em(0), _amb.UltimaMensagemDoWise(acompanhamento));

        Enviar(70, s2: 3);
        await Aguardar();
        Assert.Equal(Em(70), _amb.UltimaMensagemDoWise(acompanhamento));
    }

    [Fact]
    public async Task WiseLivre_NaoAnotaNada_NaColetaQueJaFinalizou()
    {
        var acompanhamento = await _amb.IniciarColetaAsync(_amb.MaquinaA);
        await _amb.FinalizarColetaAsync(acompanhamento);

        Enviar(0, s2: 1);
        await Aguardar();

        Assert.Null(_amb.UltimaMensagemDoWise(acompanhamento));
    }
}

/// <summary>O relógio de verdade (PeriodicTimer), movido pelo tempo simulado.</summary>
public class ColetaIotRelogioAutomaticoTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _tempo = new(new DateTimeOffset(2026, 9, 24, 11, 0, 0, TimeSpan.Zero));
    private AmbienteColetaIot _amb = null!;

    public async Task InitializeAsync()
    {
        _amb = new AmbienteColetaIot(_tempo, relogioAutomatico: true);
        await _amb.SubirAsync();
    }

    public async Task DisposeAsync() => await _amb.DisposeAsync();

    [Fact]
    public async Task SemNinguemChamar_ORelogioPercebeOWiseQueNuncaApareceu()
    {
        await _amb.IniciarColetaAsync(_amb.MaquinaA);

        await AmbienteColetaIot.AguardarAsync(() =>
        {
            _tempo.Advance(TimeSpan.FromSeconds(1)); // um tique por volta
            return _amb.Servico.Situacao(_amb.MaquinaA)?.Situacao == SituacaoMaquina.SemComunicacao;
        }, "o relógio automático marcar sem comunicação");

        Assert.Single(_amb.Periodos(_amb.MaquinaA));
    }
}
