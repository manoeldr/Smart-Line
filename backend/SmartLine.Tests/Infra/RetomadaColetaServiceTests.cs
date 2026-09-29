using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Coleta;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

public class RetomadaColetaServiceTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    // Coleta iniciada às 11:00 UTC de 24/09; meia-noite em Brasília = 03:00 UTC.
    private static readonly DateTime Virada1 = new(2026, 9, 25, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Virada2 = new(2026, 9, 26, 3, 0, 0, DateTimeKind.Utc);

    private async Task<IReadOnlyList<ColetaRetomada>> Retomar()
    {
        await using var db = _c.Banco.NovoContexto();
        var registrador = new RegistradorColeta(db);
        return await new RetomadaColetaService(db, registrador, _c.Servico(db), _c.Tempo, new OpcoesColetaIot()).RetomarAsync();
    }

    private void Reiniciar(DateTime em) => _c.Tempo.SetUtcNow(new DateTimeOffset(em));

    [Fact]
    public async Task FechaParadaNaUltimaEvidencia_EMarcaOTempoForaDoArComoSemComunicacao()
    {
        await _c.Registrar(new ParadaIniciada(Em(600), ClassificacaoParada.NaoClassificada));
        await _c.Consolidar(Em(900), garrafas: 0, brutos: new Dictionary<CanalWise, uint> { [CanalWise.S2] = 5000 });
        Reiniciar(Em(3600)); // backend caiu logo depois de 900 s e voltou 45 min depois

        var r = Assert.Single(await Retomar());

        Assert.Null(r.Erro);
        Assert.Equal((_c.Acompanhamento, _c.MaquinaLinha), (r.AcompanhamentoId, r.MaquinaLinhaId));
        Assert.Equal(Em(900), r.SemComunicacaoDesdeUtc);
        Assert.Equal(5000u, r.Contadores[CanalWise.S2]);
        Assert.Equal((Em(600), Em(900)), (Assert.Single(_c.Paradas()).Inicio, _c.Paradas()[0].Fim!.Value));
        var periodo = Assert.Single(_c.Periodos());
        Assert.Equal(Em(900), periodo.Inicio);
        Assert.Null(periodo.Fim);
    }

    [Fact]
    public async Task SemNenhumDadoAinda_EvidenciaEhOInicioDaColeta()
    {
        Reiniciar(Em(600));

        var r = Assert.Single(await Retomar());

        Assert.Equal(T0.UtcDateTime, r.SemComunicacaoDesdeUtc);
        Assert.Empty(r.Contadores);
        Assert.Equal(T0.UtcDateTime, Assert.Single(_c.Periodos()).Inicio);
    }

    [Fact]
    public async Task UltimaMensagemAnotadaNaColeta_ContaComoEvidencia()
    {
        using (var db = _c.Banco.NovoContexto())
        {
            db.Acompanhamentos.Single().UltimaMensagemWiseEm = Em(1200);
            db.SaveChanges();
        }
        Reiniciar(Em(3600));

        var r = Assert.Single(await Retomar());

        Assert.Equal(Em(1200), r.SemComunicacaoDesdeUtc);
    }

    [Fact]
    public async Task JaEstavaSemComunicacao_MantemOPeriodoOriginal()
    {
        await _c.Registrar(new ComunicacaoPerdida(Em(300)));
        await _c.Consolidar(Em(600), garrafas: 0, brutos: new Dictionary<CanalWise, uint> { [CanalWise.S2] = 10 });
        Reiniciar(Em(3600));

        var r = Assert.Single(await Retomar());

        Assert.Equal(Em(300), r.SemComunicacaoDesdeUtc);
        Assert.Equal(Em(300), Assert.Single(_c.Periodos()).Inicio);
    }

    [Fact]
    public async Task FazAsViradasQueFicaramParaTras()
    {
        await _c.Registrar(new ParadaIniciada(Em(600), _c.Classificacao("Falta de garrafas na entrada")));
        Reiniciar(Virada2.AddHours(5)); // fora do ar por quase dois dias

        await Retomar();

        var sessoes = _c.Sessoes();
        Assert.Equal(3, sessoes.Count);
        Assert.Equal((Virada1, MotivoFechamentoSessao.ViradaDoDia), (sessoes[0].Fim!.Value, sessoes[0].MotivoFechamento!.Value));
        Assert.Equal((Virada1, Virada2), (sessoes[1].Inicio, sessoes[1].Fim!.Value));
        Assert.Equal((Virada2, StatusSessao.EmAndamento), (sessoes[2].Inicio, sessoes[2].Status));
        Assert.All(sessoes, s => Assert.Equal(_c.Usuario, s.UsuarioId));

        // A parada foi fechada na última evidência antes das viradas: não atravessa os dias.
        Assert.Equal(Em(600), sessoes[0].Paradas.Single().Fim);
        Assert.Empty(sessoes[1].Paradas);
        Assert.Empty(sessoes[2].Paradas);
    }

    [Fact]
    public async Task EhIdempotente()
    {
        await _c.Registrar(new ParadaIniciada(Em(600), ClassificacaoParada.NaoClassificada));
        Reiniciar(Virada1.AddHours(1));

        await Retomar();
        var segunda = Assert.Single(await Retomar());

        Assert.Null(segunda.Erro);
        Assert.Equal(2, _c.Sessoes().Count);
        Assert.Single(_c.Periodos());
        Assert.Single(_c.Paradas());
    }

    [Fact]
    public async Task IgnoraAcompanhamentosFinalizados()
    {
        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);
        Reiniciar(Em(3600));

        Assert.Empty(await Retomar());
        Assert.Empty(_c.Periodos());
    }

    [Fact]
    public async Task RegrasInconsistentes_DevolveErro_SemDerrubarARetomada()
    {
        using (var db = _c.Banco.NovoContexto())
        {
            db.CondicoesRegra.First().Canal = null; // condição de sensor sem canal
            db.SaveChanges();
        }
        Reiniciar(Em(3600));

        var r = Assert.Single(await Retomar());

        Assert.Null(r.Configuracao);
        Assert.Contains("sem canal", r.Erro);
        Assert.Throws<InvalidOperationException>(() => r.CriarEstado());
        Assert.Single(_c.Periodos()); // o banco ficou coerente mesmo assim
    }

    [Fact]
    public async Task PontaAPonta_PrimeiraMensagemDepoisDoReinicio_FechaOPeriodoECreditaAProducao()
    {
        await _c.Consolidar(Em(900), garrafas: 3000, brutos: new Dictionary<CanalWise, uint> { [CanalWise.S2] = 5000, [CanalWise.S3] = 40 });
        Reiniciar(Em(3600));
        var estado = Assert.Single(await Retomar()).CriarEstado();

        var eventos = estado.Processar(new AmostraWise
        {
            TimestampUtc = Em(3620),
            Contadores = new Dictionary<CanalWise, uint> { [CanalWise.S2] = 7000, [CanalWise.S3] = 45 }
        });
        await _c.Registrar(eventos.ToArray());
        var pendente = eventos.OfType<ProducaoApurada>().Aggregate(ProducaoPendente.Nenhuma, (acc, p) => acc.Somar(p));
        await _c.Consolidar(Em(3620), pendente.Garrafas, pendente.Rejeito);

        Assert.Equal((Em(900), Em(3620)), (Assert.Single(_c.Periodos()).Inicio, _c.Periodos()[0].Fim!.Value));
        var ultima = _c.Sessoes().Single().Producoes.OrderBy(p => p.Hora).Last();
        Assert.Equal((5000, 5), (ultima.Quantidade, ultima.Refugo)); // 3000 + 2000 produzidas com o backend fora
    }
}
