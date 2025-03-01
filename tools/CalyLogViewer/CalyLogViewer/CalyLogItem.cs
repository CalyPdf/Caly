using Microsoft.Extensions.Logging;

namespace CalyLogViewer
{
    internal sealed record CalyLogItem(LogLevel LogLevel, EventId EventId, string Message)
    { }
}
