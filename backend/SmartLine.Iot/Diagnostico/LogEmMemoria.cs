using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SmartLine.Iot.Diagnostico;

/// <summary>Uma linha do log, como a Área do Desenvolvedor mostra.</summary>
/// <param name="Sequencia">Cresce 1 a 1 desde que o backend subiu; a tela pede "só o que veio depois de N".</param>
/// <param name="Categoria">Quem registrou (ex.: SmartLine.Iot.Coleta.ColetaIotService), sem o namespace na tela.</param>
public sealed record LinhaLog(long Sequencia, DateTime InstanteUtc, LogLevel Nivel, string Categoria, string Mensagem, string? Excecao);

/// <summary>
/// Guarda as últimas linhas do log do backend em memória (as mais antigas saem), para o
/// Desenvolvedor ver dentro do sistema sem abrir terminal. Some ao reiniciar.
/// </summary>
public sealed class LogEmMemoria : ILoggerProvider
{
    public const int CapacidadePadrao = 2000;

    private readonly int _capacidade;
    private readonly ConcurrentQueue<LinhaLog> _linhas = new();
    private long _sequencia;

    public LogEmMemoria(int capacidade = CapacidadePadrao)
    {
        _capacidade = capacidade;
    }

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void Dispose() { }

    /// <summary>Linhas depois de <paramref name="depoisDe"/> (0 = todas), da mais antiga para a mais nova, no máximo <paramref name="maximo"/>.</summary>
    public IReadOnlyList<LinhaLog> Linhas(long depoisDe = 0, LogLevel? nivelMinimo = null, string? texto = null, int maximo = 500)
    {
        IEnumerable<LinhaLog> consulta = _linhas.ToArray();
        if (depoisDe > 0) consulta = consulta.Where(l => l.Sequencia > depoisDe);
        if (nivelMinimo is { } n) consulta = consulta.Where(l => l.Nivel >= n);
        if (!string.IsNullOrWhiteSpace(texto))
            consulta = consulta.Where(l =>
                l.Mensagem.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || l.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || (l.Excecao?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false));
        // As últimas N, em ordem cronológica.
        return consulta.TakeLast(maximo).ToList();
    }

    public long UltimaSequencia => Interlocked.Read(ref _sequencia);

    internal void Registrar(LogLevel nivel, string categoria, string mensagem, Exception? excecao)
    {
        var linha = new LinhaLog(Interlocked.Increment(ref _sequencia), DateTime.UtcNow, nivel, categoria, mensagem, excecao?.ToString());
        _linhas.Enqueue(linha);
        while (_linhas.Count > _capacidade && _linhas.TryDequeue(out _)) { }
    }

    private sealed class Registrador(LogEmMemoria dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Debug e Trace fora: é o que o console mostra, não mais.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            dono.Registrar(logLevel, categoria, formatter(state, exception), exception);
        }
    }
}
