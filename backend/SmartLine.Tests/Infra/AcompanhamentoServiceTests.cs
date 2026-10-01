using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

public class AcompanhamentoServiceTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 24, 14, 0, 0, TimeSpan.Zero);

    private readonly BancoEmMemoria _banco = new();
    private readonly FakeTimeProvider _tempo = new(Agora);
    private readonly Guid _usuario;
    private readonly Guid _outroUsuario;
    private readonly Guid _maquinaCatalogo;

    public AcompanhamentoServiceTests()
    {
        using var db = _banco.NovoContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cliente" };
        var linha = new Linha { Id = Guid.NewGuid(), ClienteId = cliente.Id, Nome = "Linha 1" };
        var maquina = new Maquina { Id = Guid.NewGuid(), Nome = "Enchedora", Ativo = true };
        _usuario = Guid.NewGuid();
        _outroUsuario = Guid.NewGuid();
        _maquinaCatalogo = maquina.Id;
        db.AddRange(cliente, linha, maquina,
            new Usuario { Id = _usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor },
            new Usuario { Id = _outroUsuario, Nome = "Outro", Login = "outro", SenhaHash = "x", Nivel = NivelUsuario.Auditor });
        db.SaveChanges();
        _linha = linha.Id;
    }

    private readonly Guid _linha;
    private int _proximoIp = 20;
    private readonly Dictionary<Guid, string> _ips = new();

    public void Dispose() => _banco.Dispose();

    /// <summary>Máquina na linha e um WISE cadastrado para ela (IP que <see cref="Item"/> informa ao iniciar).</summary>
    private Guid NovaMaquinaLinha(int z = 60, decimal velocidade = 36000)
    {
        using var db = _banco.NovoContexto();
        var ml = new MaquinaLinha
        {
            Id = Guid.NewGuid(), LinhaId = _linha, MaquinaId = _maquinaCatalogo,
            VelocidadeNominal = velocidade, TempoDeteccaoParadaSegundos = z, Ativo = true
        };
        db.MaquinasLinha.Add(ml);
        _ips[ml.Id] = $"192.168.10.{++_proximoIp}";
        db.Wises.Add(new Wise { Id = Guid.NewGuid(), EnderecoIp = _ips[ml.Id], CriadoEm = Agora.UtcDateTime });
        db.SaveChanges();
        return ml.Id;
    }

    private AcompanhamentoService Servico(SmartLine.Infrastructure.Data.SmartLineDbContext db) =>
        new(db, new RegrasPadraoService(db), _tempo, new OpcoesColetaIot());

    private IniciarAcompanhamentoRequest Item(Guid maquinaLinha, params CanalMedicaoRequest[] canais) =>
        new(maquinaLinha, _ips.GetValueOrDefault(maquinaLinha, "192.168.10.200"), null, null, canais.Length > 0
            ? canais
            : [new(CanalWise.S2, 12), new(CanalWise.S3), new(CanalWise.S8), new(CanalWise.S7)]);

    private async Task<ResultadoIniciarAcompanhamento> Iniciar(Guid usuario, IniciarAcompanhamentoRequest request)
    {
        await using var db = _banco.NovoContexto();
        return await Servico(db).IniciarAsync(usuario, request);
    }

    /// <summary>Inicia e devolve o que foi criado (falha o teste se recusar).</summary>
    private async Task<AcompanhamentoIniciadoDto> IniciarOk(Guid usuario, IniciarAcompanhamentoRequest request)
    {
        var r = await Iniciar(usuario, request);
        Assert.True(r.Sucesso, r.Erro);
        return r.Iniciado!;
    }

    private async Task<ResultadoFinalizacao> Finalizar(Guid acompanhamento, Guid usuario, bool podeOutros = false)
    {
        await using var db = _banco.NovoContexto();
        return await Servico(db).FinalizarAsync(acompanhamento, usuario, podeOutros);
    }

    // ── Iniciar ─────────────────────────────────────────────────────

    [Fact]
    public async Task Iniciar_CriaAcompanhamentoCanaisSessaoEProducaoInicialZerada()
    {
        var ml = NovaMaquinaLinha(z: 45, velocidade: 40000);

        var iniciado = await IniciarOk(_usuario, Item(ml));

        using var db = _banco.NovoContexto();
        var a = db.Acompanhamentos.Include(x => x.Canais).Single();
        Assert.Equal(iniciado.AcompanhamentoId, a.Id);
        Assert.Equal(_usuario, a.UsuarioId);
        Assert.Equal(Agora.UtcDateTime, a.IniciadoEm);
        Assert.Null(a.FinalizadoEm);
        Assert.Equal(45, a.TempoDeteccaoParadaSegundos);
        Assert.Equal(12, a.Canais.Single(c => c.Canal == CanalWise.S2).Multiplicador);
        Assert.Equal(4, a.Canais.Count);

        var s = db.Sessoes.Include(x => x.Producoes).Single();
        Assert.Equal(iniciado.SessaoId, s.Id);
        Assert.Equal(a.Id, s.AcompanhamentoId);
        Assert.Equal(TipoColeta.SemiAutomatico, s.TipoColeta);
        Assert.Equal(StatusSessao.EmAndamento, s.Status);
        Assert.Equal(_usuario, s.UsuarioId);
        Assert.Equal(40000, s.VelocidadeNominal);
        var p = Assert.Single(s.Producoes);
        Assert.Equal((0, 0, Agora.UtcDateTime), (p.Quantidade, p.Refugo, p.Hora));
    }

    [Fact]
    public async Task Iniciar_ComProducaoAteEntao_LeituraInicialEOContadorDaMaquina()
    {
        var ml = NovaMaquinaLinha();

        await IniciarOk(_usuario, Item(ml) with { ProducaoInicial = 152340 });

        using var db = _banco.NovoContexto();
        var p = Assert.Single(db.Producoes);
        Assert.Equal((152340, 0), (p.Quantidade, p.Refugo));
    }

    [Fact]
    public async Task Iniciar_ProducaoAteEntaoNegativa_Recusa()
    {
        var ml = NovaMaquinaLinha();

        var r = await Iniciar(_usuario, Item(ml) with { ProducaoInicial = -1 });

        Assert.False(r.Sucesso);
        Assert.Contains("negativa", r.Erro);
    }

    [Fact]
    public async Task Iniciar_MaquinaQueNaoMedeProducao_IgnoraAProducaoAteEntao()
    {
        var ml = NovaMaquinaLinha();
        using (var db = _banco.NovoContexto())
        {
            db.MaquinasLinha.Single(m => m.Id == ml).MedeProducao = false;
            db.SaveChanges();
        }

        await IniciarOk(_usuario, Item(ml) with { ProducaoInicial = 500 });

        using var ctx = _banco.NovoContexto();
        Assert.Equal(0, Assert.Single(ctx.Producoes).Quantidade);
    }

    [Fact]
    public async Task Iniciar_MultiplicadorEmSensorDeEstado_ViraUm()
    {
        var ml = NovaMaquinaLinha();

        await Iniciar(_usuario, Item(ml, new(CanalWise.S2), new(CanalWise.S8, 99)));

        using var db = _banco.NovoContexto();
        Assert.Equal(1, db.AcompanhamentoCanais.Single(c => c.Canal == CanalWise.S8).Multiplicador);
    }

    [Fact]
    public async Task VariasMaquinasEmParalelo_UmaPorVez_MesmoUsuario()
    {
        var enchedora = NovaMaquinaLinha();
        var lavadora = NovaMaquinaLinha();

        await IniciarOk(_usuario, Item(enchedora));
        await IniciarOk(_usuario, Item(lavadora));

        using var db = _banco.NovoContexto();
        Assert.Equal(2, db.Acompanhamentos.Count(x => x.UsuarioId == _usuario && x.FinalizadoEm == null));
        Assert.Equal(2, db.Sessoes.Count(x => x.UsuarioId == _usuario && x.Status == StatusSessao.EmAndamento));
    }

    [Theory]
    [InlineData(null, "Informe o IP do WISE")]
    [InlineData("   ", "Informe o IP do WISE")]
    [InlineData("192.168.010.021", "IP do WISE inválido")]
    [InlineData("wise", "IP do WISE inválido")]
    public async Task Iniciar_SemIpDoWiseOuInvalido_Recusa_ENadaEhCriado(string? ip, string erro)
    {
        var ml = NovaMaquinaLinha();

        var r = await Iniciar(_usuario, Item(ml) with { EnderecoIpWise = ip });

        Assert.False(r.Sucesso);
        Assert.Contains(erro, r.Erro);
        using var db = _banco.NovoContexto();
        Assert.Empty(db.Acompanhamentos);
        Assert.Empty(db.Sessoes);
    }

    [Fact]
    public async Task Iniciar_WiseNaoCadastrado_Recusa()
    {
        var ml = NovaMaquinaLinha();

        var r = await Iniciar(_usuario, Item(ml) with { EnderecoIpWise = "192.168.10.99" });

        Assert.False(r.Sucesso);
        Assert.Contains("O WISE 192.168.10.99 não está cadastrado", r.Erro);
    }

    [Fact]
    public async Task Iniciar_GravaOIpDoWiseNaFormaCanonica()
    {
        var ml = NovaMaquinaLinha();
        using (var cadastro = _banco.NovoContexto())
        {
            cadastro.Wises.Add(new Wise { Id = Guid.NewGuid(), EnderecoIp = "192.168.10.50", CriadoEm = Agora.UtcDateTime });
            cadastro.SaveChanges();
        }

        await IniciarOk(_usuario, Item(ml) with { EnderecoIpWise = " ::ffff:192.168.10.50 " });

        using var db = _banco.NovoContexto();
        Assert.Equal("192.168.10.50", db.Acompanhamentos.Single().EnderecoIpWise);
    }

    [Fact]
    public async Task Iniciar_WiseEmUsoEmOutraMedicao_RecusaDizendoOnde()
    {
        var enchedora = NovaMaquinaLinha();
        var rotuladora = NovaMaquinaLinha();
        await IniciarOk(_usuario, Item(enchedora));

        var r = await Iniciar(_outroUsuario, Item(rotuladora) with { EnderecoIpWise = _ips[enchedora] });

        Assert.False(r.Sucesso);
        Assert.Contains($"O WISE {_ips[enchedora]} está em uso na medição da Enchedora (Linha 1 · Cliente)", r.Erro);
    }

    [Fact]
    public async Task DepoisDeFinalizar_OWiseFicaLivre_EPodeIrParaOutraMaquina_EAColetaGuardaOIp()
    {
        var enchedora = NovaMaquinaLinha();
        var rotuladora = NovaMaquinaLinha();
        var ip = _ips[enchedora];
        var primeira = (await IniciarOk(_usuario, Item(enchedora))).AcompanhamentoId;
        await Finalizar(primeira, _usuario);

        var segunda = (await IniciarOk(_usuario, Item(rotuladora) with { EnderecoIpWise = ip })).AcompanhamentoId;

        using var db = _banco.NovoContexto();
        Assert.Equal(ip, db.Acompanhamentos.Single(a => a.Id == primeira).EnderecoIpWise);
        Assert.Equal(ip, db.Acompanhamentos.Single(a => a.Id == segunda).EnderecoIpWise);
    }

    [Fact]
    public async Task Banco_NaoDeixaOMesmoWiseEmDuasColetasEmAndamento()
    {
        var enchedora = NovaMaquinaLinha();
        var rotuladora = NovaMaquinaLinha();
        await IniciarOk(_usuario, Item(enchedora));

        using var db = _banco.NovoContexto();
        db.Acompanhamentos.Add(new Acompanhamento
        {
            Id = Guid.NewGuid(), MaquinaLinhaId = rotuladora, UsuarioId = _usuario, IniciadoEm = Agora.UtcDateTime,
            EnderecoIpWise = _ips[enchedora], TempoDeteccaoParadaSegundos = 60
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Iniciar_SemContadorDeProducao_RecusaComMensagemLimpa()
    {
        var ml = NovaMaquinaLinha();

        var r = await Iniciar(_usuario, Item(ml, new(CanalWise.S3), new(CanalWise.S8)));

        Assert.False(r.Sucesso);
        Assert.Contains("contador de produção", r.Erro);
        Assert.DoesNotContain("Parameter", r.Erro);
    }

    [Fact]
    public async Task Iniciar_MaquinaInexistente_Recusa()
    {
        Assert.False((await Iniciar(_usuario, Item(Guid.NewGuid()))).Sucesso);
    }

    [Fact]
    public async Task Iniciar_MaquinaJaEmColeta_Recusa()
    {
        var ml = NovaMaquinaLinha();
        await Iniciar(_usuario, Item(ml));

        var r = await Iniciar(_outroUsuario, Item(ml));

        Assert.False(r.Sucesso);
        Assert.Contains("em andamento", r.Erro);
    }

    [Fact]
    public async Task Iniciar_MaquinaComSessaoManualAberta_Recusa()
    {
        var ml = NovaMaquinaLinha();
        using (var db = _banco.NovoContexto())
        {
            db.Sessoes.Add(new Sessao { Id = Guid.NewGuid(), MaquinaLinhaId = ml, UsuarioId = _outroUsuario, Inicio = Agora.UtcDateTime, Status = StatusSessao.EmAndamento });
            db.SaveChanges();
        }

        var r = await Iniciar(_usuario, Item(ml));

        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task OutraMaquinaDaMesmaLinhaEmMedicaoManual_NaoImpede()
    {
        var paletizadora = NovaMaquinaLinha();
        var enchedora = NovaMaquinaLinha();
        using (var db = _banco.NovoContexto())
        {
            db.Sessoes.Add(new Sessao { Id = Guid.NewGuid(), MaquinaLinhaId = paletizadora, UsuarioId = _outroUsuario, Inicio = Agora.UtcDateTime, Status = StatusSessao.EmAndamento });
            db.SaveChanges();
        }

        await IniciarOk(_usuario, Item(enchedora));
    }

    // ── Configuração ────────────────────────────────────────────────

    [Fact]
    public async Task Configuracao_UsaCanaisDoAcompanhamento_EOZCopiadoNoInicio()
    {
        var ml = NovaMaquinaLinha(z: 45);
        var id = (await IniciarOk(_usuario, Item(ml))).AcompanhamentoId;
        using (var db = _banco.NovoContexto())
        {
            db.MaquinasLinha.Single(x => x.Id == ml).TempoDeteccaoParadaSegundos = 300; // cadastro mudou depois
            db.SaveChanges();
        }

        await using var leitura = _banco.NovoContexto();
        var config = await Servico(leitura).CarregarConfiguracaoAsync(id);

        Assert.Equal(TimeSpan.FromSeconds(45), config.TempoDeteccaoParada);
        Assert.Equal(12, config.Multiplicadores[CanalWise.S2]);
        Assert.True(config.SensoresLidos.SetEquals([CanalWise.S7, CanalWise.S8]));
        Assert.Equal(4, config.Regras.Count); // padrão do catálogo, criado na hora por não existir
    }

    [Fact]
    public async Task Configuracao_RegrasCustomizadasDaMaquinaDaLinha_TemPrioridadeSobreOCatalogo()
    {
        var ml = NovaMaquinaLinha();
        var motivoCustom = Guid.NewGuid();
        using (var db = _banco.NovoContexto())
        {
            db.MotivosParada.Add(new MotivoParada { Id = motivoCustom, MaquinaId = _maquinaCatalogo, Nome = "Só nesta linha", Tipo = TipoParada.Externa });
            db.ConjuntosRegras.Add(new ConjuntoRegras
            {
                Id = Guid.NewGuid(), MaquinaLinhaId = ml,
                Regras = [new RegraClassificacao
                {
                    Id = Guid.NewGuid(), Prioridade = 1, Nome = "custom", MotivoParadaId = motivoCustom,
                    Condicoes = [new CondicaoRegra { Id = Guid.NewGuid(), Tipo = TipoCondicao.SensorEmAlarme, Canal = CanalWise.S7 }]
                }]
            });
            db.SaveChanges();
        }
        var id = (await IniciarOk(_usuario, Item(ml))).AcompanhamentoId;

        await using var leitura = _banco.NovoContexto();
        var config = await Servico(leitura).CarregarConfiguracaoAsync(id);

        Assert.Equal(motivoCustom, Assert.Single(config.Regras).MotivoParadaId);
    }

    // ── Finalizar ───────────────────────────────────────────────────

    [Fact]
    public async Task Finalizar_FechaSessaoParadaEPeriodoAbertos()
    {
        var ml = NovaMaquinaLinha();
        var ini = await IniciarOk(_usuario, Item(ml));
        using (var db = _banco.NovoContexto())
        {
            db.Paradas.Add(new Parada { Id = Guid.NewGuid(), SessaoId = ini.SessaoId, Inicio = Agora.UtcDateTime.AddMinutes(10) });
            db.PeriodosSemComunicacao.Add(new PeriodoSemComunicacao { Id = Guid.NewGuid(), MaquinaLinhaId = ml, Inicio = Agora.UtcDateTime.AddMinutes(20) });
            db.SaveChanges();
        }
        _tempo.Advance(TimeSpan.FromHours(1));

        Assert.Equal(ResultadoFinalizacao.Finalizado, await Finalizar(ini.AcompanhamentoId, _usuario));

        var fim = Agora.UtcDateTime.AddHours(1);
        using var leitura = _banco.NovoContexto();
        var a = leitura.Acompanhamentos.Single();
        Assert.Equal((fim, _usuario), (a.FinalizadoEm!.Value, a.FinalizadoPorId!.Value));
        var s = leitura.Sessoes.Single();
        Assert.Equal((StatusSessao.Finalizada, fim, MotivoFechamentoSessao.Manual), (s.Status, s.Fim!.Value, s.MotivoFechamento!.Value));
        Assert.Equal(fim, leitura.Paradas.Single().Fim);
        Assert.Equal(fim, leitura.PeriodosSemComunicacao.Single().Fim);
    }

    [Fact]
    public async Task Finalizar_OutroUsuario_SoComPermissao()
    {
        var ml = NovaMaquinaLinha();
        var id = (await IniciarOk(_usuario, Item(ml))).AcompanhamentoId;

        Assert.Equal(ResultadoFinalizacao.SemPermissao, await Finalizar(id, _outroUsuario));
        Assert.Equal(ResultadoFinalizacao.Finalizado, await Finalizar(id, _outroUsuario, podeOutros: true));
    }

    [Fact]
    public async Task Finalizar_DuasVezesOuInexistente()
    {
        var ml = NovaMaquinaLinha();
        var id = (await IniciarOk(_usuario, Item(ml))).AcompanhamentoId;
        await Finalizar(id, _usuario);

        Assert.Equal(ResultadoFinalizacao.JaFinalizado, await Finalizar(id, _usuario));
        Assert.Equal(ResultadoFinalizacao.NaoEncontrado, await Finalizar(Guid.NewGuid(), _usuario));
    }

    [Fact]
    public async Task DepoisDeFinalizar_PodeIniciarDeNovoNaMesmaMaquina()
    {
        var ml = NovaMaquinaLinha();
        var id = (await IniciarOk(_usuario, Item(ml))).AcompanhamentoId;
        await Finalizar(id, _usuario);

        await IniciarOk(_usuario, Item(ml));
    }
}
