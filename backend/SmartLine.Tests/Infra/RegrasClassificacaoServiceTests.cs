using SmartLine.Core.Entities.Global;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

public class RegrasClassificacaoServiceTests : IDisposable
{
    private const string Falta = "Falta de garrafas na entrada";
    private const string AcumuloMinimo = "Abaixo do acúmulo mínimo";

    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private async Task<ResultadoCadastro<ConjuntoRegrasDto>> Com(Func<RegrasClassificacaoService, Task<ResultadoCadastro<ConjuntoRegrasDto>>> acao)
    {
        await using var db = _c.Banco.NovoContexto();
        return await acao(new RegrasClassificacaoService(db, new RegrasPadraoService(db), _c.Tempo));
    }

    private Task<ResultadoCadastro<ConjuntoRegrasDto>> Catalogo() => Com(s => s.DoCatalogoAsync(_c.MaquinaCatalogo));

    private Task<ResultadoCadastro<ConjuntoRegrasDto>> SalvarCatalogo(params SalvarRegraRequest[] regras) =>
        Com(s => s.SalvarDoCatalogoAsync(_c.MaquinaCatalogo, new SalvarRegrasRequest(regras)));

    private static SalvarRegraRequest Sensor(Guid motivo, CanalWise canal, Guid? id = null, bool ativa = true) =>
        new(id, null, motivo, ativa, [new CondicaoDto(TipoCondicao.SensorEmAlarme, canal)]);

    private Guid NovoMotivo(string nome, TipoParada tipo, bool ativo = true, Guid? maquina = null)
    {
        using var db = _c.Banco.NovoContexto();
        var motivo = new MotivoParada { Id = Guid.NewGuid(), MaquinaId = maquina ?? _c.MaquinaCatalogo, Nome = nome, Tipo = tipo, Ativo = ativo };
        db.MotivosParada.Add(motivo);
        db.SaveChanges();
        return motivo.Id;
    }

    // ── Catálogo ────────────────────────────────────────────────────

    [Fact]
    public async Task Catalogo_TrazAsQuatroPadrao_NaOrdem()
    {
        var r = (await Catalogo()).Valor!;

        Assert.False(r.Personalizado);
        Assert.Equal("Enchedora", r.Maquina);
        Assert.Equal(new[] { (1, CanalWise.S8), (2, CanalWise.S1), (3, CanalWise.S7), (4, CanalWise.S4) },
            r.Regras.Select(x => (x.Prioridade, Assert.Single(x.Condicoes).Canal!.Value)));
        Assert.All(r.Regras, x => Assert.Equal((TipoParada.Externa, true), (x.Tipo, x.Ativa)));
    }

    [Fact]
    public async Task Catalogo_MaquinaNova_GanhaOPadrao_EInexistenteNaoEncontrada()
    {
        var rotuladora = Guid.NewGuid();
        using (var db = _c.Banco.NovoContexto())
        {
            db.Maquinas.Add(new Maquina { Id = rotuladora, Nome = "Rotuladora", Ativo = true });
            db.SaveChanges();
        }

        Assert.Equal(4, (await Com(s => s.DoCatalogoAsync(rotuladora))).Valor!.Regras.Count);
        Assert.True((await Com(s => s.DoCatalogoAsync(Guid.NewGuid()))).NaoEncontrado);
    }

    [Fact]
    public async Task SalvarCatalogo_ReordenaEditaCriaERemove()
    {
        var antes = (await Catalogo()).Valor!.Regras;
        var s8 = antes.Single(r => r.Condicoes[0].Canal == CanalWise.S8);
        var s1 = antes.Single(r => r.Condicoes[0].Canal == CanalWise.S1);
        var paradaLonga = NovoMotivo("Parada longa sem causa aparente", TipoParada.Interna);

        var r = await SalvarCatalogo(
            Sensor(s1.MotivoParadaId, CanalWise.S1, id: s1.Id),
            new SalvarRegraRequest(null, "Mais de 10 min com entrada normal", paradaLonga, true,
                [new CondicaoDto(TipoCondicao.SensorForaDeAlarme, CanalWise.S8), new CondicaoDto(TipoCondicao.TempoParadaMinimo, TempoMinimoSegundos: 600)]),
            Sensor(s8.MotivoParadaId, CanalWise.S8, id: s8.Id, ativa: false));

        var depois = r.Valor!.Regras;
        Assert.Equal(new[] { (1, s1.Id, true), (2, depois[1].Id, true), (3, s8.Id, false) },
            depois.Select(x => (x.Prioridade, x.Id, x.Ativa)));
        Assert.DoesNotContain(depois[1].Id, antes.Select(x => x.Id));
        Assert.Equal(("Mais de 10 min com entrada normal", TipoParada.Interna), (depois[1].Nome, depois[1].Tipo));
        Assert.Equal(new[] { new CondicaoDto(TipoCondicao.SensorForaDeAlarme, CanalWise.S8), new CondicaoDto(TipoCondicao.TempoParadaMinimo, null, 600) },
            depois[1].Condicoes);
        Assert.Equal(AcumuloMinimo, depois[0].Nome); // nome vazio = nome do motivo

        using var db = _c.Banco.NovoContexto();
        Assert.Equal(3, db.RegrasClassificacao.Count(x => x.ConjuntoRegras.MaquinaId == _c.MaquinaCatalogo));
        Assert.Equal(4, db.CondicoesRegra.Count(x => x.RegraClassificacao.ConjuntoRegras.MaquinaId == _c.MaquinaCatalogo));
    }

    [Fact]
    public async Task SalvarCatalogo_Invalida_DizQualRegra_ENaoMudaNada()
    {
        var falta = _c.Motivo(Falta);
        var deOutraMaquina = NovoMotivo("Outro", TipoParada.Interna, maquina: CriarOutraMaquina());

        Assert.Equal("Regra 2: S2 é contador; as condições usam só os sensores de estado (S1, S4, S7, S8).",
            (await SalvarCatalogo(Sensor(falta, CanalWise.S8), Sensor(falta, CanalWise.S2))).Erro);
        Assert.StartsWith("Regra 1: informe ao menos uma condição",
            (await SalvarCatalogo(new SalvarRegraRequest(null, null, falta, true, []))).Erro);
        Assert.StartsWith("Regra 1: condição de tempo",
            (await SalvarCatalogo(new SalvarRegraRequest(null, null, falta, true, [new CondicaoDto(TipoCondicao.TempoParadaMinimo, TempoMinimoSegundos: 0)]))).Erro);
        Assert.Equal("Regra 1: condição de sensor sem sensor.",
            (await SalvarCatalogo(new SalvarRegraRequest(null, null, falta, true, [new CondicaoDto(TipoCondicao.SensorEmAlarme)]))).Erro);
        Assert.Equal("Regra 1: motivo não encontrado para esta máquina.",
            (await SalvarCatalogo(Sensor(deOutraMaquina, CanalWise.S8))).Erro);

        Assert.Equal(4, (await Catalogo()).Valor!.Regras.Count);
    }

    [Fact]
    public async Task SalvarCatalogo_MotivoInativo_SoNumaRegraDesativada()
    {
        var antigo = NovoMotivo("Antigo", TipoParada.Externa, ativo: false);

        Assert.StartsWith("Regra 1: o motivo \"Antigo\" está inativo",
            (await SalvarCatalogo(Sensor(antigo, CanalWise.S8))).Erro);
        Assert.False(Assert.Single((await SalvarCatalogo(Sensor(antigo, CanalWise.S8, ativa: false))).Valor!.Regras).MotivoAtivo);
    }

    [Fact]
    public async Task RestaurarPadrao_VoltaAsQuatro_ComOsMesmosIds()
    {
        var originais = (await Catalogo()).Valor!.Regras.Select(r => r.Id).ToList();
        await SalvarCatalogo(Sensor(_c.Motivo(Falta), CanalWise.S8));

        var r = await Com(s => s.RestaurarPadraoAsync(_c.MaquinaCatalogo));

        Assert.Equal(originais, r.Valor!.Regras.Select(x => x.Id));
    }

    // ── Máquina da linha ────────────────────────────────────────────

    [Fact]
    public async Task MaquinaLinha_SemPersonalizacao_UsaAsDoCatalogo()
    {
        var r = (await Com(s => s.DaMaquinaLinhaAsync(_c.MaquinaLinha))).Valor!;

        Assert.False(r.Personalizado);
        Assert.Equal((await Catalogo()).Valor!.Regras.Select(x => x.Id), r.Regras.Select(x => x.Id));
    }

    [Fact]
    public async Task MaquinaLinha_Personalizar_NaoMexeNoCatalogo_EValeParaAColeta()
    {
        var catalogo = (await Catalogo()).Valor!.Regras;
        var s1 = catalogo.Single(r => r.Condicoes[0].Canal == CanalWise.S1);

        // Copia só a do S1 (com o Id do catálogo, como a tela faria) para esta máquina.
        var r = await Com(s => s.SalvarDaMaquinaLinhaAsync(_c.MaquinaLinha,
            new SalvarRegrasRequest([Sensor(s1.MotivoParadaId, CanalWise.S1, id: s1.Id)])));

        var personalizada = Assert.Single(r.Valor!.Regras);
        Assert.True(r.Valor.Personalizado);
        Assert.Equal(_c.MaquinaLinha, r.Valor.MaquinaLinhaId);
        Assert.NotEqual(s1.Id, personalizada.Id);
        Assert.Equal(4, (await Catalogo()).Valor!.Regras.Count);
        Assert.Equal(s1.MotivoParadaId, Assert.Single((await _c.Configuracao()).Regras).MotivoParadaId);

        var removida = await Com(s => s.RemoverPersonalizacaoAsync(_c.MaquinaLinha));

        Assert.False(removida.Valor!.Personalizado);
        Assert.Equal(4, (await _c.Configuracao()).Regras.Count);
    }

    [Fact]
    public async Task MaquinaLinha_Inexistente()
    {
        Assert.True((await Com(s => s.DaMaquinaLinhaAsync(Guid.NewGuid()))).NaoEncontrado);
        Assert.True((await Com(s => s.SalvarDaMaquinaLinhaAsync(Guid.NewGuid(), new SalvarRegrasRequest([])))).NaoEncontrado);
        Assert.True((await Com(s => s.RemoverPersonalizacaoAsync(Guid.NewGuid()))).NaoEncontrado);
    }

    private Guid CriarOutraMaquina()
    {
        using var db = _c.Banco.NovoContexto();
        var m = new Maquina { Id = Guid.NewGuid(), Nome = "Lavadora", Ativo = true };
        db.Maquinas.Add(m);
        db.SaveChanges();
        return m.Id;
    }
}
