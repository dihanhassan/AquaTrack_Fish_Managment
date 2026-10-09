using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Ai;

public record GrowthForecast(
    int FishStockId,
    string PondName,
    string SpeciesName,
    decimal CurrentAvgWeightGram,
    decimal DailyGainGram,
    decimal PredictedWeightGram,
    DateOnly PredictedOn,
    decimal PredictedBiomassKg,
    int? DaysToTarget,
    DateOnly? EstimatedHarvestDate,
    decimal Confidence,
    string Rationale);

public interface IGrowthPredictor
{
    /// <summary>AI-2: projects weight and biomass forward from the batch's own sampling history.</summary>
    Task<IReadOnlyList<GrowthForecast>> ForecastAsync(
        int horizonDays = 30, int? pondId = null, CancellationToken ct = default);
}

public class GrowthPredictor(IApplicationDbContext db) : IGrowthPredictor
{
    public async Task<IReadOnlyList<GrowthForecast>> ForecastAsync(
        int horizonDays = 30, int? pondId = null, CancellationToken ct = default)
    {
        var query = db.FishStocks
            .Include(s => s.Pond)
            .Include(s => s.FishType)
            .Include(s => s.GrowthRecords)
            .Where(s => s.Status == StockStatus.Active && s.CurrentQuantity > 0);

        if (pondId.HasValue)
            query = query.Where(s => s.PondId == pondId.Value);

        var stocks = await query.AsNoTracking().ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var forecasts = new List<GrowthForecast>(stocks.Count);

        foreach (var stock in stocks)
        {
            // x is days since stocking, y is sampled weight. The stocking event itself is the first point.
            var points = new List<(double X, double Y)>
            {
                (0d, (double)stock.InitialAvgWeightGram),
            };

            points.AddRange(stock.GrowthRecords
                .OrderBy(g => g.RecordDate)
                .Select(g => ((double)(g.RecordDate.DayNumber - stock.StockDate.DayNumber), (double)g.AvgWeightGram)));

            var daysElapsed = today.DayNumber - stock.StockDate.DayNumber;
            var currentWeight = stock.CurrentAvgWeightGram > 0 ? stock.CurrentAvgWeightGram : stock.InitialAvgWeightGram;

            var fit = Statistics.Fit(points);

            decimal dailyGain;
            decimal confidence;
            string basis;

            if (fit is { Slope: > 0 } line)
            {
                dailyGain = (decimal)line.Slope;
                // A straight line through few points is easy to fit and easy to be wrong about.
                confidence = (decimal)Math.Clamp(line.RSquared * Math.Min(points.Count / 5d, 1d), 0.2d, 0.95d);
                basis = $"Fitted to {points.Count} sampling points (R2 {line.RSquared:F2}).";
            }
            else if (daysElapsed > 0 && currentWeight > stock.InitialAvgWeightGram)
            {
                // Not enough samples for a regression, so fall back to average gain since stocking.
                dailyGain = (currentWeight - stock.InitialAvgWeightGram) / daysElapsed;
                confidence = 0.4m;
                basis = $"Averaged over {daysElapsed} days since stocking (too few samples to fit a trend).";
            }
            else
            {
                dailyGain = 0m;
                confidence = 0.15m;
                basis = "No growth history yet. Record a weight sample to enable prediction.";
            }

            var predictedWeight = Math.Round(Math.Max(currentWeight, currentWeight + dailyGain * horizonDays), 1);
            var predictedBiomass = Math.Round(stock.CurrentQuantity * predictedWeight / 1000m, 2);

            int? daysToTarget = null;
            DateOnly? harvestDate = null;
            var target = stock.FishType?.TargetWeightGram;

            if (target.HasValue && dailyGain > 0 && target.Value > currentWeight)
            {
                daysToTarget = (int)Math.Ceiling((target.Value - currentWeight) / dailyGain);
                if (daysToTarget is > 0 and < 3650)
                    harvestDate = today.AddDays(daysToTarget.Value);
            }

            var rationale =
                $"Current {currentWeight:N0} g, gaining about {dailyGain:N2} g/day. {basis} " +
                (harvestDate.HasValue
                    ? $"Reaches the {target:N0} g target around {harvestDate:dd MMM yyyy}."
                    : "No harvest target set for this species.");

            forecasts.Add(new GrowthForecast(
                stock.Id,
                stock.Pond?.Name ?? "-",
                stock.FishType?.Name ?? "-",
                currentWeight,
                Math.Round(dailyGain, 2),
                predictedWeight,
                today.AddDays(horizonDays),
                predictedBiomass,
                daysToTarget,
                harvestDate,
                confidence,
                rationale));
        }

        return [.. forecasts.OrderByDescending(f => f.PredictedBiomassKg)];
    }
}
