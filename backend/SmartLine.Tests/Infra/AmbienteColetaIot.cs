using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Coleta;
using SmartLine.Infrastructure.Data;
using SmartLine.Infrastructure.Repositories;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;

namespace SmartLine.Tests.Infra;

/// <summary>
/// O motor da coleta montado como no app (mesmos serviços, escopo por
/// operação), sobre banco em arquivo. Duas enchedoras numa linha e dois WISE
/// cadastrados: <see cref="MaquinaA"/> mede com o <see cref="IpA"/> e
/// <see cref="MaquinaB"/> com o <see cref="IpB"/> (escolhidos ao iniciar a
/// coleta, que é quando o WISE fica associado à máquina). Nenhuma coleta iniciada.
/// </summary>
internal sealed class AmbienteColetaIot : IAsyncDisposable
{
    public const string IpA = "127.0.0.21";
    public const string IpB = "127.0.0.22";

    private readonly ServiceProvider _servicos;
    private bool _iniciado;

    /// <param name="relogioAutomatico">
    /// Falso (padrão): o relógio só roda quando o teste chama
    /// <see cref="ColetaIotService.VerificarAgoraAsync"/>, o que deixa o teste determinístico.
    /// </param>
    public AmbienteColetaIot(TimeProvider tempo, int tempoDeteccaoParadaSegundos = 60, bool relogioAutomatico = false)
    {
        Tempo = tempo;
        using (var db = Banco.NovoContexto())
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cliente" };
            var linha = new Linha { Id = Guid.NewGuid(), ClienteId = cliente.Id, Nome = "Linha 1" };
            var catalogo = new Maquina { Id = Guid.NewGuid(), Nome = "Enchedora", Ativo = true };
            db.AddRange(cliente, linha, catalogo,
                new Usuario { Id = Usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor });
            db.Wises.AddRange(
                new Wise { Id = Guid.NewGuid(), EnderecoIp = IpA, CriadoEm = DateTime.UtcNow },
                new Wise { Id = Guid.NewGuid(), EnderecoIp = IpB, CriadoEm = DateTime.UtcNow });
            foreach (var maquina in new[] { MaquinaA, MaquinaB })
            {
                db.Add(new MaquinaLinha
                {
                    Id = maquina, LinhaId = linha.Id, MaquinaId = catalogo.Id, VelocidadeNominal = 36000, Ativo = true,
                    TempoDeteccaoParadaSegundos = tempoDeteccaoParadaSegundos
                });
            }
            db.SaveChanges();
        }

        var servicos = new ServiceCollection();
        servicos.AddScoped(_ => Banco.NovoContexto());
        servicos.AddScoped<IRegrasPadraoService, RegrasPadraoService>();
        servicos.AddScoped<IAcompanhamentoService, AcompanhamentoService>();
        servicos.AddScoped<IRegistradorColeta, RegistradorColeta>();
        servicos.AddScoped<IRetomadaColetaService, RetomadaColetaService>();
        servicos.AddScoped<ILocalizadorColetaIot, LocalizadorColetaIot>();
        servicos.AddSingleton(tempo);
        var opcoes = new OpcoesColetaIot
        {
            IntervaloVerificacao = relogioAutomatico ? TimeSpan.FromSeconds(1) : Timeout.InfiniteTimeSpan
        };
        servicos.AddSingleton(opcoes);
        _servicos = servicos.BuildServiceProvider();

        Servico = new ColetaIotService(Caixa, _servicos.GetRequiredService<IServiceScopeFactory>(), tempo, opcoes, Log);
    }

    public BancoEmArquivo Banco { get; } = new();
    public TimeProvider Tempo { get; }
    public CaixaDeEntradaMqtt Caixa { get; } = new();
    public LogEmLista<ColetaIotService> Log { get; } = new();
    public ColetaIotService Servico { get; }
    public Guid Usuario { get; } = Guid.NewGuid();
    public Guid MaquinaA { get; } = Guid.NewGuid();
    public Guid MaquinaB { get; } = Guid.NewGuid();

    /// <summary>Canais da medição de sempre: S2, S3 e os quatro sensores de estado.</summary>
    public static CanalMedicaoRequest[] CanaisPadrao =>
        [new(CanalWise.S2), new(CanalWise.S3), new(CanalWise.S1), new(CanalWise.S4), new(CanalWise.S7), new(CanalWise.S8)];

    /// <summary>Sobe o motor (faz a retomada) e espera ficar pronto para consumir.</summary>
    public async Task SubirAsync()
    {
        _iniciado = true;
        await Servico.StartAsync(CancellationToken.None);
        await Servico.RetomadaConcluida.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>Desliga o motor como no fim do app (grava o pendente).</summary>
    public async Task PararAsync()
    {
        _iniciado = false;
        await Servico.StopAsync(CancellationToken.None);
    }

    /// <summary>Cadastra mais um WISE (os de <see cref="IpA"/> e <see cref="IpB"/> já vêm cadastrados).</summary>
    public void CadastrarWise(string ip) =>
        Alterar(db => db.Wises.Add(new Wise { Id = Guid.NewGuid(), EnderecoIp = ip, CriadoEm = DateTime.UtcNow }));

    /// <summary>WISE de sempre da máquina: <see cref="IpA"/> na A, <see cref="IpB"/> na B.</summary>
    public string IpDa(Guid maquina) => maquina == MaquinaA ? IpA : IpB;

    /// <param name="ip">WISE informado ao iniciar; nulo = o de sempre da máquina.</param>
    public async Task<Guid> IniciarColetaAsync(Guid maquina, string? ip = null)
    {
        await using var escopo = _servicos.CreateAsyncScope();
        var r = await escopo.ServiceProvider.GetRequiredService<IAcompanhamentoService>()
            .IniciarAsync(Usuario, new IniciarAcompanhamentoRequest(maquina, ip ?? IpDa(maquina), null, null, CanaisPadrao));
        Assert.True(r.Sucesso, r.Erro);
        return r.Iniciado!.AcompanhamentoId;
    }

    public async Task FinalizarColetaAsync(Guid acompanhamento)
    {
        await using var escopo = _servicos.CreateAsyncScope();
        var r = await escopo.ServiceProvider.GetRequiredService<IAcompanhamentoService>()
            .FinalizarAsync(acompanhamento, Usuario, false);
        Assert.Equal(ResultadoFinalizacao.Finalizado, r);
    }

    /// <summary>
    /// Mensagem do WISE como o broker enfileira. Sensores no normal por padrão
    /// (invertidos: S1/S8 em 0, S4/S7 em 1).
    /// </summary>
    public void Enviar(string ip, DateTime recebidaEmUtc, uint s2, uint s3 = 0, bool faltaGarrafas = false)
    {
        var payload = $$"""
            {"s":1,"t":"x","q":192,"c":0,"di1":false,"di2":{{s2}},"di3":{{s3}},"di4":true,"di5":0,"di6":0,"di7":true,"di8":{{(faltaGarrafas ? "true" : "false")}}}
            """;
        Enviar(ip, recebidaEmUtc, payload);
    }

    public void Enviar(string ip, DateTime recebidaEmUtc, string payload, string topico = "Advantech/00D0C9FA1234/data") =>
        Caixa.Publicar(new MensagemMqtt($"WISE-{ip}", ip, topico, Encoding.UTF8.GetBytes(payload), recebidaEmUtc));

    /// <summary>Espera o motor dar conta de <paramref name="total"/> mensagens (processadas ou descartadas).</summary>
    public Task AguardarMensagensAsync(long total) =>
        AguardarAsync(() => Servico.MensagensProcessadas + Servico.MensagensDescartadas >= total,
            $"o motor tratar {total} mensagens");

    public static async Task AguardarAsync(Func<bool> condicao, string oQue)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"Tempo esgotado esperando {oQue}.");
            await Task.Delay(10);
        }
    }

    public List<Parada> Paradas(Guid acompanhamento)
    {
        using var db = Banco.NovoContexto();
        return db.Paradas
            .Include(p => p.Motivo)
            .Where(p => p.Sessao.AcompanhamentoId == acompanhamento)
            .OrderBy(p => p.Inicio)
            .ToList();
    }

    public List<Sessao> Sessoes(Guid acompanhamento)
    {
        using var db = Banco.NovoContexto();
        return db.Sessoes
            .Include(s => s.Producoes)
            .Where(s => s.AcompanhamentoId == acompanhamento)
            .OrderBy(s => s.Inicio)
            .ToList();
    }

    /// <summary>Última leitura de produção da sessão (a de maior hora).</summary>
    public static Producao UltimaLeitura(Sessao sessao) => sessao.Producoes.OrderBy(p => p.Hora).Last();

    public async Task<IReadOnlyList<WiseEmMedicao>> WisesEmMedicaoAsync()
    {
        await using var escopo = _servicos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ILocalizadorColetaIot>().WisesEmMedicaoAsync();
    }

    /// <summary>Última mensagem anotada na coleta.</summary>
    public DateTime? UltimaMensagemDoWise(Guid acompanhamento)
    {
        using var db = Banco.NovoContexto();
        return db.Acompanhamentos.Single(a => a.Id == acompanhamento).UltimaMensagemWiseEm;
    }

    public List<PeriodoSemComunicacao> Periodos(Guid maquina)
    {
        using var db = Banco.NovoContexto();
        return db.PeriodosSemComunicacao.Where(p => p.MaquinaLinhaId == maquina).OrderBy(p => p.Inicio).ToList();
    }

    public void Alterar(Action<SmartLineDbContext> alteracao)
    {
        using var db = Banco.NovoContexto();
        alteracao(db);
        db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        if (_iniciado)
            await Servico.StopAsync(CancellationToken.None);
        Servico.Dispose();
        await _servicos.DisposeAsync();
        Banco.Dispose();
    }
}

/// <summary>Logger que guarda as mensagens, para conferir avisos.</summary>
internal sealed class LogEmLista<T> : ILogger<T>
{
    private readonly List<(LogLevel Nivel, string Texto)> _entradas = new();

    public IReadOnlyList<(LogLevel Nivel, string Texto)> Entradas
    {
        get { lock (_entradas) return _entradas.ToList(); }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entradas) _entradas.Add((logLevel, formatter(state, exception)));
    }
}
