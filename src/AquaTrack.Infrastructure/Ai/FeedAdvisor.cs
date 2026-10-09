using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Ai;

public record FeedAdvice(
    int FishStockId,
    string PondName,
    string SpeciesName,
    int FishCount,
    decimal AvgWeightGram,
    decimal BiomassKg,
    decimal FeedRatePercent,
    decimal RecommendedKgPerDay,
    decimal ActualAvgKgPerDay,
    decimal Confidence,
    string Rationale);

public interface IFeedAdvisor
{
    /// <summary>AI-1: daily feed recommendation for every active batch, or one pond when filtered.</summary>
    Task<IReadOnlyList<FeedAdvice>> RecommendAsync(int? pondId = null, CancellationToken ct = default);
}

/// <summary>
/// Implements the standard percentage-of-body-weight feeding table used in pond aquaculture:
/// small fish eat a far higher share of their body weight than large fish do.
/// </summary>
public class FeedAdvisor(AquaTrackDbContext db) : IFeedAdvisor
{
    /// <summary>Upper weight bound (grams) paired with the daily feed rate as a percent of biomass.</summary>
    private static readonly (decimal MaxWeightGram, decimal RatePercent)[] FeedingTable =
    [
        (5m, 10m),
        (20m, 8m),
        (50m, 6m),
        (100m, 5m),
        (200m, 4m),
        (400m, 3m),
        (700m, 2.5m),
        (1000m, 2m),
        (decimal.MaxValue, 1.5m),
    ];

    internal static decimal RateForWeight(decimal avgWeightGram)
        => FeedingTable.First(row => avgWeightGram <= row.MaxWeightGram).RatePercent;

    public async Task<IReadOnlyList<FeedAdvice>> RecommendAsync(int? pondId = null, CancellationToken ct = default)
    {
        var query = db.FishStocks
            .Include(s => s.Pond)
            .Include(s => s.FishType)
            .Where(s => s.Status == StockStatus.Active && s.CurrentQuantity > 0);

        if (pondId.HasValue)
            query = query.Where(s => s.PondId == pondId.Value);

        var stocks = await query.AsNoTracking().ToListAsync(ct);
        if (stocks.Count == 0) return [];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var since = today.AddDays(-7);
        var pondIds = stocks.Select(s => s.PondId).Distinct().ToList();

        // One round trip for the last week of feeding, used to compare advice against practice.
        var recentFeed = await db.FeedUsages
            .Where(u => pondIds.Contains(u.PondId) && u.UsageDate >= since && u.UsageDate <= today)
            .GroupBy(u => u.PondId)
            .Select(g => new { PondId = g.Key, Total = g.Sum(u => u.Quantity), Days = g.Select(u => u.UsageDate).Distinct().Count() })
            .ToDictionaryAsync(x => x.PondId, x => x, ct);

        var advice = new List<FeedAdvice>(stocks.Count);

        foreach (var stock in stocks)
        {
            var avgWeight = stock.CurrentAvgWeightGram > 0 ? stock.CurrentAvgWeightGram : stock.InitialAvgWeightGram;
            var biomassKg = stock.CurrentQuantity * avgWeight / 1000m;

            // Species-specific rate wins when it has been configured, otherwise the standard table.
            var tableRate = RateForWeight(avgWeight);
            var speciesRate = stock.FishType?.DefaultFeedRatePercent ?? 0m;
            var rate = speciesRate > 0 ? (speciesRate + tableRate) / 2m : tableRate;

            var recommended = Math.Round(biomassKg * rate / 100m, 2);

            recentFeed.TryGetValue(stock.PondId, out var recent);
            var actualPerDay = recent is { Days: > 0 } ? Math.Round(recent.Total / recent.Days, 2) : 0m;

            // Confidence rises once the batch has been sampled; an unsampled batch is a guess.
            var hasGrowthSample = stock.CurrentAvgWeightGram > 0 && stock.CurrentAvgWeightGram != stock.InitialAvgWeightGram;
            var confidence = hasGrowthSample ? 0.85m : 0.55m;

            var rationale =
                $"{stock.CurrentQuantity:N0} fish at {avgWeight:N0} g gives {biomassKg:N1} kg biomass. " +
                $"At {rate:N1}% of body weight per day that is {recommended:N2} kg. " +
                (actualPerDay > 0
                    ? $"Last 7 days averaged {actualPerDay:N2} kg/day."
                    : "No feeding recorded in the last 7 days.");

            advice.Add(new FeedAdvice(
                stock.Id,
                stock.Pond?.Name ?? "-",
                stock.FishType?.Name ?? "-",
                stock.CurrentQuantity,
                avgWeight,
                Math.Round(biomassKg, 2),
                rate,
                recommended,
                actualPerDay,
                confidence,
                rationale));
        }

        return [.. advice.OrderByDescending(a => a.RecommendedKgPerDay)];
    }
}
