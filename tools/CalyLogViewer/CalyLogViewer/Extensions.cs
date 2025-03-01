using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace CalyLogViewer
{
    internal static class Extensions
    {
        public static LogEventLevel ToLogEventLevel(this LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return LogEventLevel.Fatal;

                case LogLevel.Error:
                    return LogEventLevel.Error;

                case LogLevel.Warning:
                    return LogEventLevel.Warning;

                case LogLevel.Information:
                    return LogEventLevel.Information;

                case LogLevel.Debug:
                    return LogEventLevel.Debug;

                default:
                    case LogLevel.None:
                    return LogEventLevel.Verbose;
            }
        }
    }
}
