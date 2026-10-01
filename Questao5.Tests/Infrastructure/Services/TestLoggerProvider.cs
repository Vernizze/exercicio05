using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Questao5.Tests.Infrastructure.Services;

internal sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<TestLogEntry> entries = new();

    public IReadOnlyCollection<TestLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, entries);
    }

    public void Dispose()
    {
    }

    private sealed class TestLogger : ILogger
    {
        private readonly string categoryName;
        private readonly ConcurrentQueue<TestLogEntry> entries;

        public TestLogger(string categoryName, ConcurrentQueue<TestLogEntry> entries)
        {
            this.categoryName = categoryName;
            this.entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new TestLogEntry(
                logLevel,
                eventId,
                categoryName,
                formatter(state, exception)));
        }
    }
}