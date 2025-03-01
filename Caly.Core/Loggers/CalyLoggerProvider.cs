using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Loggers
{
    [ProviderAlias("PipeLogger")]
    public sealed class CalyLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, CalyLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
        
        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new CalyLogger(name));

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
