using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Enums;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

public class RegrasPadraoServiceTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();

    public void Dispose() => _banco.Dispose();

    private Guid NovaMaquina(string nome = "Enchedora", bool ativo = true, params MotivoParada[] motivos)
    {
        using var db = _banco.NovoContexto();
        var maquina = new Maquina { Id = Guid.NewGuid(), Nome = nome, Ativo = ativo };
        foreach (var m in motivos) { m.MaquinaId = maquina.Id; maquina.MotivosParada.Add(m); }
        db.Maquinas.Add(maquina);
        db.SaveChanges();
        return maquina.Id;
    }

    private async Task<int> GarantirTodas()
    {
        await using var db = _banco.NovoContexto();
        return await new RegrasPadraoService(db).GarantirParaTodasAsync();
    }

    private List<RegraClassificacao> RegrasDa(Guid maquinaId)
    {
        using var db = _banco.NovoContexto();
        return db.RegrasClassificacao
            .Include(r => r.Condicoes)
            .Include(r => r.MotivoParada)
            .Where(r => r.ConjuntoRegras.MaquinaId == maquinaId)
            .OrderBy(r => r.Prioridade)
            .ToList();
    }

    [Fact]
    public async Task CriaAsQuatroRegrasComMotivosExternos_ParaCadaMaquinaAtiva()
    {
        var enchedora = NovaMaquina("Enchedora");
        var lavadora = NovaMaquina("Lavadora");
        var inativa = NovaMaquina("Antiga", ativo: false);

        Assert.Equal(2, await GarantirTodas());

        foreach (var maquina in new[] { enchedora, lavadora })
        {
            var regras = RegrasDa(maquina);
            Assert.Equal(RegrasPadraoIot.Todas.Select(p => p.NomeMotivo), regras.Select(r => r.MotivoParada.Nome));
            Assert.Equal(RegrasPadraoIot.Todas.Select(p => p.Sensor), regras.Select(r => Assert.Single(r.Condicoes).Canal!.Value));
            Assert.All(regras, r =>
            {
                Assert.True(r.Ativa);
                Assert.Equal(TipoParada.Externa, r.MotivoParada.Tipo);
                Assert.Equal(maquina, r.MotivoParada.MaquinaId);
                Assert.Equal(TipoCondicao.SensorEmAlarme, r.Condicoes.Single().Tipo);
            });
        }
        Assert.Empty(RegrasDa(inativa));
    }

    [Fact]
    public async Task EhIdempotente()
    {
        NovaMaquina();
        await GarantirTodas();

        Assert.Equal(0, await GarantirTodas());

        using var db = _banco.NovoContexto();
        Assert.Equal(1, db.ConjuntosRegras.Count());
        Assert.Equal(4, db.RegrasClassificacao.Count());
        Assert.Equal(4, db.MotivosParada.Count());
    }

    [Fact]
    public async Task NaoRecriaNemSobrescreveRegrasEditadas()
    {
        var maquina = NovaMaquina();
        await GarantirTodas();
        using (var db = _banco.NovoContexto())
        {
            db.RegrasClassificacao.Remove(db.RegrasClassificacao.First(r => r.Prioridade == 4));
            db.RegrasClassificacao.First(r => r.Prioridade == 1).Ativa = false;
            db.SaveChanges();
        }

        await GarantirTodas();
        await using (var db = _banco.NovoContexto())
            Assert.False(await new RegrasPadraoService(db).GarantirAsync(maquina));

        var regras = RegrasDa(maquina);
        Assert.Equal(3, regras.Count);
        Assert.False(regras.Single(r => r.Prioridade == 1).Ativa);
    }

    [Fact]
    public async Task ReaproveitaMotivoExternoComOMesmoNome_SemDuplicar()
    {
        var existente = new MotivoParada { Id = Guid.NewGuid(), Nome = "Falta de garrafas na entrada", Tipo = TipoParada.Externa };
        var maquina = NovaMaquina(motivos: new[] { existente });

        await GarantirTodas();

        Assert.Equal(existente.Id, RegrasDa(maquina).Single(r => r.Prioridade == 1).MotivoParadaId);
        using var db = _banco.NovoContexto();
        Assert.Equal(1, db.MotivosParada.Count(m => m.Nome == "Falta de garrafas na entrada"));
    }

    [Fact]
    public async Task ReaproveitaMotivoPadraoRenomeado_QueVeioDeImportacao()
    {
        // No PC central o motivo padrão foi renomeado; a estação importou máquina + motivos
        // (mesmo Id determinístico), mas as regras não vêm na importação.
        var maquinaId = Guid.NewGuid();
        var idPadrao = RegrasPadraoIot.IdDeterministico(maquinaId, "motivo:falta-garrafas-entrada");
        using (var db = _banco.NovoContexto())
        {
            db.Maquinas.Add(new Maquina { Id = maquinaId, Nome = "Enchedora", Ativo = true });
            db.MotivosParada.Add(new MotivoParada { Id = idPadrao, MaquinaId = maquinaId, Nome = "Sem garrafa (S8)", Tipo = TipoParada.Externa });
            db.SaveChanges();
        }

        await GarantirTodas(); // antes da correção: violação de chave primária

        Assert.Equal(idPadrao, RegrasDa(maquinaId).Single(r => r.Prioridade == 1).MotivoParadaId);
    }

    [Fact]
    public async Task IdsSaoOsMesmosEmQualquerComputador()
    {
        var maquinaId = Guid.NewGuid();
        List<Guid> IdsGerados()
        {
            using var banco = new BancoEmMemoria();
            using (var db = banco.NovoContexto())
            {
                db.Maquinas.Add(new Maquina { Id = maquinaId, Nome = "Enchedora", Ativo = true });
                db.SaveChanges();
            }
            using (var db = banco.NovoContexto())
                new RegrasPadraoService(db).GarantirParaTodasAsync().GetAwaiter().GetResult();
            using var leitura = banco.NovoContexto();
            return leitura.MotivosParada.Select(m => m.Id)
                .Concat(leitura.RegrasClassificacao.Select(r => r.Id))
                .Concat(leitura.CondicoesRegra.Select(c => c.Id))
                .Concat(leitura.ConjuntosRegras.Select(c => c.Id))
                .OrderBy(id => id).ToList();
        }

        Assert.Equal(IdsGerados(), IdsGerados());
    }

    [Fact]
    public void IdDeterministico_MudaComABaseEComAChave_ETemVersao5()
    {
        var a = Guid.NewGuid();
        var id = RegrasPadraoIot.IdDeterministico(a, "x");

        Assert.Equal(id, RegrasPadraoIot.IdDeterministico(a, "x"));
        Assert.NotEqual(id, RegrasPadraoIot.IdDeterministico(a, "y"));
        Assert.NotEqual(id, RegrasPadraoIot.IdDeterministico(Guid.NewGuid(), "x"));
        Assert.Equal(5, id.Version);
    }

    [Fact]
    public async Task RegrasGravadas_ClassificamNoMotorComoEsperado()
    {
        var maquina = NovaMaquina();
        await GarantirTodas();
        var regras = MapeadorRegras.ParaDominio(RegrasDa(maquina));
        var motivoFalta = RegrasDa(maquina).Single(r => r.Prioridade == 1).MotivoParadaId;

        // S8 = 1 (sem garrafas, sensor invertido) e S7 = 0 (saída bloqueada): vence a prioridade 1.
        var estados = new Dictionary<CanalWise, bool> { [CanalWise.S8] = true, [CanalWise.S7] = false };
        var classificacao = MotorRegras.Classificar(regras, new ContextoParada(estados, TimeSpan.FromMinutes(1)));

        Assert.Equal(motivoFalta, classificacao.MotivoParadaId);
        Assert.Equal(TipoParada.Externa, classificacao.Tipo);
    }

    [Fact]
    public async Task Mapeador_IgnoraRegraDesativada()
    {
        var maquina = NovaMaquina();
        await GarantirTodas();
        var gravadas = RegrasDa(maquina);
        gravadas.Single(r => r.Prioridade == 1).Ativa = false;

        var regras = MapeadorRegras.ParaDominio(gravadas);

        Assert.Equal(3, regras.Count);
        Assert.DoesNotContain(regras, r => r.Prioridade == 1);
    }
}
