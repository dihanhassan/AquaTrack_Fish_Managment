using AquaTrack.Domain.Entities;
using AquaTrack.Application.Ai;
using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

public interface INotificationService
{
    /// <summary>Runs the anomaly detector and stores anything new as a notification.</summary>
    Task<int> RefreshAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Notification>> GetRecentAsync(int take = 20, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(int id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}

public class NotificationService(IApplicationDbContext db, IAnomalyDetector detector) : INotificationService
{
    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        var anomalies = await detector.DetectAsync(ct);
        if (anomalies.Count == 0) return 0;

        var keys = anomalies.Select(a => a.DedupeKey).ToList();

        // An alert that is already on screen and unread must not be raised again.
        var openKeys = await db.Notifications
            .Where(n => n.DedupeKey != null && keys.Contains(n.DedupeKey) && !n.IsRead)
            .Select(n => n.DedupeKey!)
            .ToListAsync(ct);

        var fresh = anomalies
            .Where(a => !openKeys.Contains(a.DedupeKey))
            .Select(a => new Notification
            {
                Type = a.Type,
                Severity = a.Severity,
                Title = a.Title,
                Message = a.Message,
                Link = a.Link,
                DedupeKey = a.DedupeKey,
                RaisedAtUtc = DateTime.UtcNow,
            })
            .ToList();

        if (fresh.Count == 0) return 0;

        db.Notifications.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        return fresh.Count;
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(int take = 20, CancellationToken ct = default)
        => await db.Notifications
            .OrderByDescending(n => n.RaisedAtUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
        => db.Notifications.CountAsync(n => !n.IsRead, ct);

    public async Task MarkReadAsync(int id, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null || notification.IsRead) return;

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        await db.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);
    }
}
