using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SentinelAgente.Tests.Connection;

/// <summary>Guarda o texto de cada linha de log, para o teste conferir o que o agente disse.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Lines.Enqueue(formatter(state, exception));
}
