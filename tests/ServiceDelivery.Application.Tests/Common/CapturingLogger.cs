using Microsoft.Extensions.Logging;

namespace ServiceDelivery.Application.Tests.Common;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> test double that records every entry's
/// <see cref="LogLevel"/>, rendered message, and exception into an in-memory list.
/// Single-purpose by design (ISP/SRP): it exists only so tests can assert the
/// severity contract of code under test — preferred over Moq's brittle
/// <c>ILogger.Log&lt;TState&gt;</c> verification.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public readonly record struct Entry(LogLevel Level, string Message, Exception? Exception);

    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries => _entries;

    public bool HasEntryAt(LogLevel level) => _entries.Exists(e => e.Level == level);

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _entries.Add(new Entry(logLevel, formatter(state, exception), exception));
}
