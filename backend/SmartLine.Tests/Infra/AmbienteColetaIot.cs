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
/// operação), sobre banco em arquivo. Duas enchedoras numa linha, cada uma com
/// seu WISE: <see cref="MaquinaA"/> no IP <see cref="IpA"/> e
/// <see cref="MaquinaB"/> no <see cref="IpB"/>. Nenhuma coleta iniciada.
/// </summary>
internal sealed class AmbienteColetaIot : IAsyncDisposable
{
    public const string IpA = "127.0.0.21";
    public const string IpB = "127.0.0.22";

    private readonly ServiceProvider _servicos;
    private bool _iniciado;

    public AmbienteColetaIot(TimeProvider tempo, int tempoDeteccaoParadaSegundos = 60)
    {
        Tempo = tempo;
        using (var db = Banco.NovoContexto())
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cliente" };
            var linha = new Linha { Id = Guid.NewGuid(), ClienteId = cliente.Id, Nome = "Linha 1" };
            var catalogo = new Maquina { Id = Guid.NewGuid(), Nome = "Enchedora", Ativo = true };
            db.AddRange(cliente, linha, catalogo,
                new Usuario { Id = Usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor });
            foreach (var (maquina, ip) in new[] { (MaquinaA, IpA), (MaquinaB, IpB) })
            {
                db.AddRange(
                    new MaquinaLinha
                    {
                        Id = maquina, LinhaId = linha.Id, MaquinaId = catalogo.Id, VelocidadeNominal = 36000, Ativo = true,
                        TempoDeteccaoParadaSegundos = tempoDeteccaoParadaSegundos
                    },
                    new DispositivoIot { Id = Guid.NewGuid(), MaquinaLinhaId = maquina, Nome = $"WISE {ip}", EnderecoIp = ip });
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
        servicos.AddSingleton(new OpcoesColetaIot());
        _servicos = servicos.BuildServiceProvider();

        Servico = new ColetaIotService(Caixa, _servicos.GetRequiredService<IServiceScopeFactory>(), Log);
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

    public async Task<Guid> IniciarColetaAsync(Guid maquina)
    {
        await using var escopo = _servicos.CreateAsyncScope();
        var r = await escopo.ServiceProvider.GetRequiredService<IAcompanhamentoService>()
            .IniciarAsync(Usuario, new IniciarAcompanhamentoRequest(maquina, null, null, CanaisPadrao));
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
