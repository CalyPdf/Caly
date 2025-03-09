using System;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Loggers
{
    public sealed class NoOpCalyLogger : ILogger
    {
        public static readonly ILogger Instance = new NoOpCalyLogger();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // No op
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return default!;
        }
    }
}
