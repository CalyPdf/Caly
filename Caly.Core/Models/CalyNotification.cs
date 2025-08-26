using Avalonia.Controls.Notifications;

namespace Caly.Core.Models
{
    public sealed class CalyNotification
    {
        public string? Title { get; init; }
        public string? Message { get; init; }
        public NotificationType Type { get; init; }
    }
}
