using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Ai;

public record Anomaly(
    NotificationType Type,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string DedupeKey,
    string? Link,
    double Score);

public interface IAnomalyDetector
{
    /// <summary>AI-4: scans recent activity for values that are out of line with the farm's own history.</summary>
    Task<IReadOnlyList<Anomaly>> DetectAsync(CancellationToken ct = default);
}

/// <summary>
/// Everything here compares the farm against its own baseline rather than an external benchmark,
/// so a small farm and a large one both get sensible alerts.
/// </summary>
public class AnomalyDetector(AquaTrackDbContext db, ISettingsService settings) : IAnomalyDetector
{
    private const int BaselineDays = 30;
    private const int RecentDays = 7;

    public async Task<IReadOnlyList<Anomaly>> DetectAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var sigma = await settings.GetDecimalAsync(SettingKeys.CostSpikeSigma, 2m, ct);
        var mortalityThreshold = await settings.GetDecimalAsync(SettingKeys.HighMortalityThresholdPercent, 2m, ct);
        var expiryDays = await settings.GetIntAsync(SettingKeys.MedicineExpiryWarningDays, 30, ct);

        var found = new List<Anomaly>();

        found.AddRange(await DetectFeedCostSpikeAsync(today, (double)sigma, ct));
        found.AddRange(await DetectMortalitySpikeAsync(today, mortalityThreshold, ct));
        found.AddRange(await DetectMedicineUsageSpikeAsync(today, (double)sigma, ct));
        found.AddRange(await DetectLowStockAsync(ct));
        found.AddRange(await DetectExpiringMedicineAsync(today, expiryDays, ct));
        found.AddRange(await DetectMaintenanceDueAsync(today, ct));

        return [.. found.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Score)];
    }

    private async Task<List<Anomaly>> DetectFeedCostSpikeAsync(DateOnly today, double sigma, CancellationToken ct)
    {
        var baselineStart = today.AddDays(-BaselineDays);
        var recentStart = today.AddDays(-RecentDays);

        var daily = await db.FeedUsages
            .Where(u => u.UsageDate >= baselineStart && u.UsageDate <= today)
            .GroupBy(u => u.UsageDate)
            .Select(g => new { Date = g.Key, Cost = g.Sum(u => u.TotalCost) })
            .ToListAsync(ct);

        var baseline = daily.Where(d => d.Date < recentStart).Select(d => (double)d.Cost).ToList();
        var recent = daily.Where(d => d.Date >= recentStart).Select(d => (double)d.Cost).ToList();

        // Fewer than a week of baseline days is not enough to call anything unusual.
        if (baseline.Count < 7 || recent.Count == 0) return [];

        var recentAverage = Statistics.Mean(recent);
        var z = Statistics.ZScore(recentAverage, baseline);
        if (z < sigma) return [];

        var baselineAverage = Statistics.Mean(baseline);
        var increase = baselineAverage > 0 ? (recentAverage - baselineAverage) / baselineAverage * 100 : 0;

        return
        [
            new Anomaly(
                NotificationType.CostSpike,
                z >= sigma * 1.5 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                "Feed cost is unusually high",
                $"Feed cost averaged {recentAverage:N0} per day over the last {RecentDays} days, " +
                $"against {baselineAverage:N0} in the prior baseline - about {increase:N0}% higher.",
                $"CostSpike:Feed:{today:yyyyMM}",
                "/Reports/Feed",
                z),
        ];
    }

    private async Task<List<Anomaly>> DetectMortalitySpikeAsync(
        DateOnly today, decimal thresholdPercent, CancellationToken ct)
    {
        var recentStart = today.AddDays(-RecentDays);

        var rows = await db.FishMortalities
            .Include(m => m.FishStock).ThenInclude(s => s!.Pond)
            .Where(m => m.RecordDate >= recentStart && m.RecordDate <= today)
            .GroupBy(m => new { m.FishStockId, PondName = m.FishStock!.Pond!.Name, m.FishStock.InitialQuantity })
            .Select(g => new { g.Key.FishStockId, g.Key.PondName, g.Key.InitialQuantity, Dead = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        var alerts = new List<Anomaly>();

        foreach (var row in rows)
        {
            if (row.InitialQuantity <= 0) continue;

            var percent = (decimal)row.Dead / row.InitialQuantity * 100m;
            if (percent < thresholdPercent) continue;

            alerts.Add(new Anomaly(
                NotificationType.HighMortality,
                percent >= thresholdPercent * 2 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                $"High mortality in {row.PondName}",
                $"{row.Dead:N0} fish died in the last {RecentDays} days - {percent:N1}% of the stocked batch. " +
                "Check water quality and recent treatments.",
                $"HighMortality:{row.FishStockId}:{today:yyyyMMdd}",
                $"/FishStocks/Details/{row.FishStockId}",
                (double)percent));
        }

        return alerts;
    }

    private async Task<List<Anomaly>> DetectMedicineUsageSpikeAsync(DateOnly today, double sigma, CancellationToken ct)
    {
        var baselineStart = today.AddDays(-BaselineDays);
        var recentStart = today.AddDays(-RecentDays);

        var daily = await db.MedicineUsages
            .Where(u => u.UsageDate >= baselineStart && u.UsageDate <= today)
            .GroupBy(u => u.UsageDate)
            .Select(g => new { Date = g.Key, Cost = g.Sum(u => u.TotalCost) })
            .ToListAsync(ct);

        var baseline = daily.Where(d => d.Date < recentStart).Select(d => (double)d.Cost).ToList();
        var recent = daily.Where(d => d.Date >= recentStart).Select(d => (double)d.Cost).ToList();

        if (baseline.Count < 7 || recent.Count == 0) return [];

        var recentAverage = Statistics.Mean(recent);
        var z = Statistics.ZScore(recentAverage, baseline);
        if (z < sigma) return [];

        return
        [
            new Anomaly(
                NotificationType.CostSpike,
                NotificationSeverity.Warning,
                "Medicine usage is unusually high",
                $"Medicine cost averaged {recentAverage:N0} per day recently, well above the usual " +
                $"{Statistics.Mean(baseline):N0}. Unusual treatment levels often follow a disease outbreak.",
                $"CostSpike:Medicine:{today:yyyyMM}",
                "/Reports/Medicine",
                z),
        ];
    }

    private async Task<List<Anomaly>> DetectLowStockAsync(CancellationToken ct)
    {
        var alerts = new List<Anomaly>();

        var feed = await db.FeedItems
            .Where(f => f.IsActive && f.ReorderLevel > 0 && f.CurrentStock <= f.ReorderLevel)
            .Select(f => new { f.Id, f.Name, f.CurrentStock, f.ReorderLevel })
            .ToListAsync(ct);

        alerts.AddRange(feed.Select(f => new Anomaly(
            NotificationType.LowStock,
            f.CurrentStock <= 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
            $"Feed running low: {f.Name}",
            $"{f.CurrentStock:N2} left, at or below the reorder level of {f.ReorderLevel:N2}.",
            $"LowStock:Feed:{f.Id}",
            $"/FeedItems/Details/{f.Id}",
            1d)));

        var medicine = await db.Medicines
            .Where(m => m.IsActive && m.ReorderLevel > 0 && m.CurrentStock <= m.ReorderLevel)
            .Select(m => new { m.Id, m.Name, m.CurrentStock, m.ReorderLevel })
            .ToListAsync(ct);

        alerts.AddRange(medicine.Select(m => new Anomaly(
            NotificationType.LowStock,
            m.CurrentStock <= 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
            $"Medicine running low: {m.Name}",
            $"{m.CurrentStock:N2} left, at or below the reorder level of {m.ReorderLevel:N2}.",
            $"LowStock:Medicine:{m.Id}",
            $"/Medicines/Details/{m.Id}",
            1d)));

        return alerts;
    }

    private async Task<List<Anomaly>> DetectExpiringMedicineAsync(DateOnly today, int warningDays, CancellationToken ct)
    {
        var cutoff = today.AddDays(warningDays);

        var rows = await db.Medicines
            .Where(m => m.IsActive && m.ExpiryDate != null && m.ExpiryDate <= cutoff && m.CurrentStock > 0)
            .Select(m => new { m.Id, m.Name, m.ExpiryDate, m.CurrentStock })
            .ToListAsync(ct);

        return [.. rows.Select(m =>
        {
            var expired = m.ExpiryDate!.Value < today;
            return new Anomaly(
                expired ? NotificationType.ExpiredMedicine : NotificationType.ExpiringMedicine,
                expired ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                expired ? $"Expired: {m.Name}" : $"Expiring soon: {m.Name}",
                expired
                    ? $"{m.Name} expired on {m.ExpiryDate:dd MMM yyyy} and {m.CurrentStock:N2} is still in stock. Dispose of it safely."
                    : $"{m.Name} expires on {m.ExpiryDate:dd MMM yyyy} with {m.CurrentStock:N2} in stock.",
                $"Expiry:Medicine:{m.Id}",
                $"/Medicines/Details/{m.Id}",
                expired ? 2d : 1d);
        })];
    }

    private async Task<List<Anomaly>> DetectMaintenanceDueAsync(DateOnly today, CancellationToken ct)
    {
        var cutoff = today.AddDays(7);

        var rows = await db.EquipmentMaintenances
            .Include(m => m.Equipment)
            .Where(m => m.NextDueDate != null && m.NextDueDate <= cutoff)
            .GroupBy(m => new { m.EquipmentId, Name = m.Equipment!.Name })
            .Select(g => new { g.Key.EquipmentId, g.Key.Name, Due = g.Max(m => m.NextDueDate) })
            .ToListAsync(ct);

        return [.. rows.Select(r => new Anomaly(
            NotificationType.MaintenanceDue,
            r.Due < today ? NotificationSeverity.Warning : NotificationSeverity.Info,
            $"Maintenance due: {r.Name}",
            $"Scheduled maintenance for {r.Name} is due on {r.Due:dd MMM yyyy}.",
            $"Maintenance:{r.EquipmentId}:{r.Due:yyyyMMdd}",
            $"/Equipments/Details/{r.EquipmentId}",
            1d))];
    }
}
