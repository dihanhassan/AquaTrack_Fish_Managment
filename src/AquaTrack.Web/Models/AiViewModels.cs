using AquaTrack.Application.Ai;

namespace AquaTrack.Web.Models;

public class AiInsightsViewModel
{
    public int? PondId { get; init; }
    public IReadOnlyList<SelectOption> Ponds { get; init; } = [];

    public IReadOnlyList<FeedAdvice> FeedAdvice { get; init; } = [];
    public IReadOnlyList<GrowthForecast> GrowthForecasts { get; init; } = [];
    public required FinanceForecast FinanceForecast { get; init; }
    public IReadOnlyList<Anomaly> Anomalies { get; init; } = [];

    public decimal TotalRecommendedFeedKg => FeedAdvice.Sum(a => a.RecommendedKgPerDay);
    public decimal TotalActualFeedKg => FeedAdvice.Sum(a => a.ActualAvgKgPerDay);

    /// <summary>
    /// How far recent feeding is from the recommendation, as a percentage. Null when nothing has
    /// been fed recently, because a comparison against zero is not informative.
    /// </summary>
    public decimal? FeedGapPercent => TotalActualFeedKg <= 0 || TotalRecommendedFeedKg <= 0
        ? null
        : Math.Round((TotalActualFeedKg - TotalRecommendedFeedKg) / TotalRecommendedFeedKg * 100, 1);

    public decimal PredictedBiomassKg => GrowthForecasts.Sum(f => f.PredictedBiomassKg);

    public GrowthForecast? NextHarvest => GrowthForecasts
        .Where(f => f.EstimatedHarvestDate is not null)
        .OrderBy(f => f.EstimatedHarvestDate)
        .FirstOrDefault();

    /// <summary>Renders a 0-1 confidence as the words a farmer would use.</summary>
    public static string ConfidenceLabel(decimal confidence) => confidence switch
    {
        >= 0.8m => "High",
        >= 0.6m => "Good",
        >= 0.4m => "Moderate",
        >= 0.2m => "Low",
        _ => "Indicative only",
    };

    public static string ConfidenceTone(decimal confidence) => confidence switch
    {
        >= 0.8m => "success",
        >= 0.6m => "info",
        >= 0.4m => "warning",
        _ => "neutral",
    };
}

public class AssistantViewModel
{
    public string? Question { get; init; }
    public AssistantAnswer? Answer { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    public bool HasAnswer => Answer is not null;
    public bool HasData => Answer is { Data.Count: > 0 };
}
