using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Coleta;
using SmartLine.Infrastructure.Data;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

/// <summary>
/// Uma Enchedora com WISE, numa linha, com coleta iniciada às 11:00 UTC por um
/// auditor, lendo S2 (×1), S3 e os quatro sensores de estado. Banco em memória
/// com o esquema real e relógio simulado.
/// </summary>
internal sealed class CenarioColeta : IDisposable
{
    public static readonly DateTimeOffset T0 = new(2026, 9, 24, 11, 0, 0, TimeSpan.Zero);

    public BancoEmMemoria Banco { get; } = new();
    public FakeTimeProvider Tempo { get; } = new(T0);
    public Guid Usuario { get; } = Guid.NewGuid();
    public Guid MaquinaCatalogo { get; } = Guid.NewGuid();
    public Guid MaquinaLinha { get; } = Guid.NewGuid();
    public Guid Acompanhamento { get; }
    public Guid Sessao { get; }

    public CenarioColeta()
    {
        using (var db = Banco.NovoContexto())
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cliente" };
            var linha = new Linha { Id = Guid.NewGuid(), ClienteId = cliente.Id, Nome = "Linha 1" };
            db.AddRange(
                cliente, linha,
                new Maquina { Id = MaquinaCatalogo, Nome = "Enchedora", Ativo = true },
                new Usuario { Id = Usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor },
                new MaquinaLinha { Id = MaquinaLinha, LinhaId = linha.Id, MaquinaId = MaquinaCatalogo, VelocidadeNominal = 36000, Ativo = true },
                new DispositivoIot { Id = Guid.NewGuid(), MaquinaLinhaId = MaquinaLinha, Nome = "WISE", IdentificadorMqtt = "00D0C9000001" });
            db.SaveChanges();
        }

        using (var db = Banco.NovoContexto())
        {
            var r = Servico(db).IniciarAsync(Usuario, new IniciarAcompanhamentoRequest(MaquinaLinha, null, null,
            [
                new(CanalWise.S2), new(CanalWise.S3), new(CanalWise.S1),
                new(CanalWise.S4), new(CanalWise.S7), new(CanalWise.S8)
            ])).GetAwaiter().GetResult();
            Acompanhamento = r.Iniciado!.AcompanhamentoId;
            Sessao = r.Iniciado.SessaoId;
        }
    }

    public AcompanhamentoService Servico(SmartLineDbContext db) =>
        new(db, new RegrasPadraoService(db), Tempo, new OpcoesColetaIot());

    public static DateTime Em(int segundos) => T0.UtcDateTime.AddSeconds(segundos);

    public async Task Registrar(params EventoColeta[] eventos)
    {
        await using var db = Banco.NovoContexto();
        await new RegistradorColeta(db).RegistrarAsync(Acompanhamento, eventos);
    }

    public async Task Consolidar(DateTime instante, long garrafas, long rejeito = 0, IReadOnlyDictionary<CanalWise, uint>? brutos = null)
    {
        await using var db = Banco.NovoContexto();
        await new RegistradorColeta(db).ConsolidarProducaoAsync(Acompanhamento, instante, new ProducaoPendente(garrafas, rejeito), brutos);
    }

    public async Task Virar(DateTime virada, long garrafas = 0, long rejeito = 0)
    {
        await using var db = Banco.NovoContexto();
        await new RegistradorColeta(db).VirarDiaAsync(Acompanhamento, virada, new ProducaoPendente(garrafas, rejeito), null);
    }

    public List<Sessao> Sessoes()
    {
        using var db = Banco.NovoContexto();
        return db.Sessoes
            .Include(s => s.Producoes)
            .Include(s => s.Paradas).ThenInclude(p => p.Motivo)
            .Include(s => s.Paradas).ThenInclude(p => p.HistoricoClassificacao)
            .Where(s => s.AcompanhamentoId == Acompanhamento)
            .OrderBy(s => s.Inicio)
            .ToList();
    }

    public async Task<ConfiguracaoColetaIot> Configuracao()
    {
        await using var db = Banco.NovoContexto();
        return await Servico(db).CarregarConfiguracaoAsync(Acompanhamento);
    }

    /// <summary>Id do motivo padrão pelo nome (criado ao iniciar a coleta).</summary>
    public Guid Motivo(string nome)
    {
        using var db = Banco.NovoContexto();
        return db.MotivosParada.Single(m => m.MaquinaId == MaquinaCatalogo && m.Nome == nome).Id;
    }

    public ClassificacaoParada Classificacao(string nomeMotivo)
    {
        using var db = Banco.NovoContexto();
        var regra = db.RegrasClassificacao.Include(r => r.MotivoParada)
            .Single(r => r.ConjuntoRegras.MaquinaId == MaquinaCatalogo && r.MotivoParada.Nome == nomeMotivo);
        return new ClassificacaoParada(regra.MotivoParada.Tipo, regra.MotivoParadaId, regra.Id);
    }

    public List<Parada> Paradas()
    {
        using var db = Banco.NovoContexto();
        return db.Paradas
            .Include(p => p.HistoricoClassificacao)
            .Include(p => p.Motivo)
            .Where(p => p.SessaoId == Sessao)
            .OrderBy(p => p.Inicio)
            .ToList();
    }

    public List<PeriodoSemComunicacao> Periodos()
    {
        using var db = Banco.NovoContexto();
        return db.PeriodosSemComunicacao.Where(p => p.MaquinaLinhaId == MaquinaLinha).OrderBy(p => p.Inicio).ToList();
    }

    public void Dispose() => Banco.Dispose();
}
