using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class Notification : BaseEntity
{
    public DateTime RaisedAtUtc { get; set; } = DateTime.UtcNow;
    public NotificationType Type { get; set; }
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }

    public bool IsRead { get; set; }
    /// <summary>Stable key (e.g. "LowStock:Feed:12") used to avoid re-raising the same open alert.</summary>
    public string? DedupeKey { get; set; }
}
