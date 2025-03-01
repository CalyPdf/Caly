using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Loggers
{
    public static class CalyLoggerExtensions
    {
        public static ILoggingBuilder AddCalyLogger(this ILoggingBuilder builder)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CalyLoggerProvider>());
            return builder;
        }
    }
}
