using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

/// <summary>
/// Exportar de um banco e importar em outro leva as regras de classificação (do catálogo e
/// personalizadas), os textos das entradas, o Z e "mede produção" da máquina da linha, a regra e o
/// histórico de cada parada e os trechos sem comunicação.
/// </summary>
public class ExportImportRegrasTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly BancoEmMemoria _origem = new();
    private readonly BancoEmMemoria _destino = new();

    private readonly Guid _cliente = Guid.NewGuid();
    private readonly Guid _linha = Guid.NewGuid();
    private readonly Guid _maquina = Guid.NewGuid();
    private readonly Guid _maquinaLinha = Guid.NewGuid();
    private readonly Guid _usuario = Guid.NewGuid();
    private readonly Guid _motivoFalta = Guid.NewGuid();
    private readonly Guid _motivoAcumulo = Guid.NewGuid();
    private readonly Guid _regraCatalogo = Guid.NewGuid();
    private readonly Guid _regraPersonalizada = Guid.NewGuid();
    private readonly Guid _sessao = Guid.NewGuid();
    private readonly Guid _parada = Guid.NewGuid();

    public ExportImportRegrasTests()
    {
        using var db = _origem.NovoContexto();
        db.AddRange(
            new Cliente { Id = _cliente, Nome = "Cliente" },
            new Linha { Id = _linha, ClienteId = _cliente, Nome = "Linha 1" },
            new Maquina { Id = _maquina, Nome = "Enchedora", Ativo = true },
            new Usuario { Id = _usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor },
            new MaquinaLinha
            {
                Id = _maquinaLinha, LinhaId = _linha, MaquinaId = _maquina, VelocidadeNominal = 36000, Ativo = true,
                MedeProducao = false, TempoDeteccaoParadaSegundos = 45
            },
            new MotivoParada { Id = _motivoFalta, MaquinaId = _maquina, Nome = "Falta de garrafas", Tipo = TipoParada.Externa, Ativo = true },
            new MotivoParada { Id = _motivoAcumulo, MaquinaId = _maquina, Nome = "Acúmulo", Tipo = TipoParada.Externa, Ativo = true },
            new TextoEntradaWise { Id = Guid.NewGuid(), MaquinaId = _maquina, Canal = CanalWise.S8, Nome = "Entrada", TextoAtivo = "sem garrafas", TextoNormal = "com garrafas" },
            new TextoEntradaWise { Id = Guid.NewGuid(), MaquinaId = _maquina, Canal = CanalWise.S2, Nome = "Contador" },
            new ConjuntoRegras
            {
                Id = Guid.NewGuid(), MaquinaId = _maquina,
                Regras =
                [
                    new RegraClassificacao
                    {
                        Id = _regraCatalogo, Prioridade = 1, Nome = "Falta na entrada", MotivoParadaId = _motivoFalta, Ativa = true,
                        Condicoes = [new CondicaoRegra { Id = Guid.NewGuid(), Tipo = TipoCondicao.SensorEmAlarme, Canal = CanalWise.S8 }]
                    }
                ]
            },
            new ConjuntoRegras
            {
                Id = Guid.NewGuid(), MaquinaLinhaId = _maquinaLinha,
                Regras =
                [
                    new RegraClassificacao
                    {
                        Id = _regraPersonalizada, Prioridade = 1, Nome = "Acúmulo demorado", MotivoParadaId = _motivoAcumulo, Ativa = false,
                        Condicoes =
                        [
                            new CondicaoRegra { Id = Guid.NewGuid(), Tipo = TipoCondicao.SensorEmAlarme, Canal = CanalWise.S7 },
                            new CondicaoRegra { Id = Guid.NewGuid(), Tipo = TipoCondicao.TempoParadaMinimo, TempoMinimoSegundos = 120 }
                        ]
                    }
                ]
            },
            new Sessao
            {
                Id = _sessao, MaquinaLinhaId = _maquinaLinha, UsuarioId = _usuario, Inicio = T0, Status = StatusSessao.EmAndamento,
                TipoColeta = TipoColeta.SemiAutomatico, VelocidadeNominal = 36000,
                Paradas =
                [
                    new Parada
                    {
                        Id = _parada, Inicio = T0.AddMinutes(5), Fim = T0.AddMinutes(8), MotivoId = _motivoFalta, RegraClassificacaoId = _regraCatalogo,
                        HistoricoClassificacao =
                        [
                            new HistoricoClassificacaoParada { Id = Guid.NewGuid(), MotivoAnteriorId = null, MotivoNovoId = _motivoFalta, UsuarioId = _usuario, AlteradoEm = T0.AddMinutes(6) }
                        ]
                    }
                ]
            },
            new PeriodoSemComunicacao { Id = Guid.NewGuid(), MaquinaLinhaId = _maquinaLinha, Inicio = T0.AddMinutes(20), Fim = T0.AddMinutes(25), ProducaoNaoRecuperada = true },
            new Wise { Id = Guid.NewGuid(), EnderecoIp = "192.168.10.21", Nome = "Enchedora", CriadoEm = T0 },
            new Wise { Id = Guid.NewGuid(), EnderecoIp = "192.168.10.22", CriadoEm = T0 });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _origem.Dispose();
        _destino.Dispose();
    }

    private async Task<byte[]> Exportar(bool clientes = true, bool maquinas = true, bool sessoes = true, bool usuarios = true)
    {
        await using var db = _origem.NovoContexto();
        return await new ExportImportService(db).ExportarAsync(new ExportOpcoes(clientes, maquinas, sessoes, usuarios));
    }

    private async Task<ImportResumoDto> Importar(byte[] pacote)
    {
        await using var db = _destino.NovoContexto();
        return await new ExportImportService(db).ImportarAsync(pacote);
    }

    [Fact]
    public async Task LevaRegrasTextosZEHistorico_ParaOOutroBanco()
    {
        var resumo = await Importar(await Exportar());

        Assert.Equal((2, 2, 2), (resumo.Regras, resumo.TextosEntradas, resumo.Wises));

        await using var db = _destino.NovoContexto();

        var ml = await db.MaquinasLinha.SingleAsync();
        Assert.Equal((false, 45), (ml.MedeProducao, ml.TempoDeteccaoParadaSegundos));

        var textos = await db.TextosEntradasWise.OrderBy(t => t.Canal).ToListAsync();
        Assert.Equal(new[] { CanalWise.S2, CanalWise.S8 }, textos.Select(t => t.Canal));
        Assert.Equal("sem garrafas", textos[1].TextoAtivo);

        var catalogo = await db.ConjuntosRegras.Include(c => c.Regras).ThenInclude(r => r.Condicoes).SingleAsync(c => c.MaquinaId == _maquina);
        var regra = Assert.Single(catalogo.Regras);
        Assert.Equal((_regraCatalogo, "Falta na entrada", _motivoFalta), (regra.Id, regra.Nome, regra.MotivoParadaId));
        Assert.Equal(CanalWise.S8, Assert.Single(regra.Condicoes).Canal);

        var personalizado = await db.ConjuntosRegras.Include(c => c.Regras).ThenInclude(r => r.Condicoes).SingleAsync(c => c.MaquinaLinhaId == _maquinaLinha);
        var personalizada = Assert.Single(personalizado.Regras);
        Assert.False(personalizada.Ativa);
        Assert.Equal(2, personalizada.Condicoes.Count);
        Assert.Contains(personalizada.Condicoes, c => c.Tipo == TipoCondicao.TempoParadaMinimo && c.TempoMinimoSegundos == 120);

        var parada = await db.Paradas.Include(p => p.HistoricoClassificacao).SingleAsync();
        Assert.Equal(_regraCatalogo, parada.RegraClassificacaoId);
        var historico = Assert.Single(parada.HistoricoClassificacao);
        Assert.Equal((_motivoFalta, _usuario), (historico.MotivoNovoId, historico.UsuarioId));

        var periodo = await db.PeriodosSemComunicacao.SingleAsync();
        Assert.Equal((T0.AddMinutes(20), true), (periodo.Inicio, periodo.ProducaoNaoRecuperada));

        var wises = await db.Wises.OrderBy(w => w.EnderecoIp).ToListAsync();
        Assert.Equal(new[] { "192.168.10.21", "192.168.10.22" }, wises.Select(w => w.EnderecoIp));
        Assert.Equal("Enchedora", wises[0].Nome);
    }

    [Fact]
    public async Task WiseComOMesmoIp_NoDestino_SoAtualizaONome()
    {
        // O destino já cadastrou o mesmo IP por conta própria (outro Id): não pode duplicar.
        await using (var db = _destino.NovoContexto())
        {
            db.Wises.Add(new Wise { Id = Guid.NewGuid(), EnderecoIp = "192.168.10.21", Nome = "antigo", CriadoEm = T0 });
            await db.SaveChangesAsync();
        }

        // Máquinas vão junto: a máquina da linha aponta para a do catálogo.
        await Importar(await Exportar(clientes: true, maquinas: true, sessoes: false, usuarios: false));

        await using var destino = _destino.NovoContexto();
        Assert.Equal(2, await destino.Wises.CountAsync());
        Assert.Equal("Enchedora", (await destino.Wises.SingleAsync(w => w.EnderecoIp == "192.168.10.21")).Nome);
    }

    [Fact]
    public async Task ImportarDeNovo_AtualizaERemoveRegraQueSumiuNaOrigem()
    {
        await Importar(await Exportar());

        // Na origem: regra do catálogo muda de nome e ganha uma segunda; a personalização some.
        await using (var db = _origem.NovoContexto())
        {
            var regra = await db.RegrasClassificacao.SingleAsync(r => r.Id == _regraCatalogo);
            regra.Nome = "Falta (renomeada)";
            db.RegrasClassificacao.Add(new RegraClassificacao
            {
                Id = Guid.NewGuid(), ConjuntoRegrasId = regra.ConjuntoRegrasId, Prioridade = 2, Nome = "Acúmulo", MotivoParadaId = _motivoAcumulo, Ativa = true,
                Condicoes = [new CondicaoRegra { Id = Guid.NewGuid(), Tipo = TipoCondicao.SensorEmAlarme, Canal = CanalWise.S7 }]
            });
            var personalizado = await db.ConjuntosRegras.SingleAsync(c => c.MaquinaLinhaId == _maquinaLinha);
            db.ConjuntosRegras.Remove(personalizado);
            await db.SaveChangesAsync();
        }

        await Importar(await Exportar(clientes: true, maquinas: true, sessoes: false, usuarios: false));

        await using var destino = _destino.NovoContexto();
        var catalogo = await destino.ConjuntosRegras.Include(c => c.Regras).SingleAsync(c => c.MaquinaId == _maquina);
        Assert.Equal(new[] { "Falta (renomeada)", "Acúmulo" }, catalogo.Regras.OrderBy(r => r.Prioridade).Select(r => r.Nome));
        Assert.False(await destino.ConjuntosRegras.AnyAsync(c => c.MaquinaLinhaId == _maquinaLinha));
        // A parada que apontava para a regra do catálogo continua apontando (a regra ficou).
        Assert.Equal(_regraCatalogo, (await destino.Paradas.SingleAsync()).RegraClassificacaoId);
    }

    [Fact]
    public async Task PacoteAntigo_SemOsCamposNovos_AindaEhLido()
    {
        var pacote = await Exportar();
        // Tira os campos novos do dados.json, como um pacote gerado antes desta versão
        // (a parada continua apontando para a regra, que não vem).
        using var zipOriginal = new System.IO.Compression.ZipArchive(new MemoryStream(pacote), System.IO.Compression.ZipArchiveMode.Read);
        using var leitor = new StreamReader(zipOriginal.GetEntry("dados.json")!.Open());
        var json = System.Text.Json.Nodes.JsonNode.Parse(await leitor.ReadToEndAsync())!.AsObject();
        json.Remove("PeriodosSemComunicacao");
        json.Remove("Wises");
        foreach (var m in json["Maquinas"]!.AsArray()) { m!.AsObject().Remove("Regras"); m.AsObject().Remove("TextosEntradas"); }
        foreach (var c in json["Clientes"]!.AsArray())
            foreach (var l in c!["Linhas"]!.AsArray())
                foreach (var ml in l!["Maquinas"]!.AsArray())
                { ml!.AsObject().Remove("Regras"); ml.AsObject().Remove("MedeProducao"); ml.AsObject().Remove("TempoDeteccaoParadaSegundos"); }

        using var memoria = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memoria, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            await using var escrita = zip.CreateEntry("dados.json").Open();
            await escrita.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()));
        }

        var resumo = await Importar(memoria.ToArray());

        Assert.Equal((1, 0, 0, 0), (resumo.Maquinas, resumo.Regras, resumo.TextosEntradas, resumo.Wises));
        await using var db = _destino.NovoContexto();
        Assert.Equal((true, 60), ((await db.MaquinasLinha.SingleAsync()).MedeProducao, (await db.MaquinasLinha.SingleAsync()).TempoDeteccaoParadaSegundos));
        Assert.False(await db.ConjuntosRegras.AnyAsync());
        // A parada apontava para uma regra que não veio: entra sem a regra, com o motivo.
        var parada = await db.Paradas.SingleAsync();
        Assert.Equal((null, _motivoFalta), (parada.RegraClassificacaoId, parada.MotivoId));
    }
}
