using System;
using System.Collections.Concurrent;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Loggers
{
    [ProviderAlias("PipeLogger")]
    public sealed class CalyLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, CalyLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        private readonly bool _logs;

        public CalyLoggerProvider(ISettingsService settingsService)
        {
            _logs = settingsService.GetSettings().Logs;
        }
        
        public ILogger CreateLogger(string categoryName)
        {
            if (_logs)
            {
                return _loggers.GetOrAdd(categoryName, name => new CalyLogger(name));
            }

            _loggers.Clear();
            return NoOpCalyLogger.Instance;
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
