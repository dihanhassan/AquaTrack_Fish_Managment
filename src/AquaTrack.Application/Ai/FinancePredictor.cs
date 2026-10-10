using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Ai;

public record MonthlyFinancePoint(DateOnly Month, decimal Income, decimal Expense)
{
    public decimal Profit => Income - Expense;
}

public record FinanceForecast(
    DateOnly Month,
    decimal PredictedIncome,
    decimal PredictedExpense,
    decimal PredictedProfit,
    decimal Confidence,
    string Rationale,
    IReadOnlyList<MonthlyFinancePoint> History);

public interface IFinancePredictor
{
    /// <summary>AI-3: next-month income, expense and profit from the cash book history.</summary>
    Task<FinanceForecast> ForecastNextMonthAsync(int lookbackMonths = 6, CancellationToken ct = default);
}

/// <summary>
/// Blends a linear trend with a moving average. The trend catches a farm that is scaling up or down;
/// the moving average keeps a single unusual month from dominating the answer.
/// </summary>
public class FinancePredictor(IApplicationDbContext db) : IFinancePredictor
{
    public async Task<FinanceForecast> ForecastNextMonthAsync(
        int lookbackMonths = 6, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var from = currentMonth.AddMonths(-lookbackMonths);

        var rows = await db.CashTransactions
            .Where(t => t.TransactionDate >= from && t.TransactionDate < currentMonth)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Direction })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Direction, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        // Only complete months are used, so the part-finished current month cannot drag the trend down.
        var history = Enumerable.Range(0, lookbackMonths)
            .Select(offset => from.AddMonths(offset))
            .Select(month => new MonthlyFinancePoint(
                month,
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Direction == CashDirection.In)
                    .Sum(r => r.Total),
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Direction == CashDirection.Out)
                    .Sum(r => r.Total)))
            .ToList();

        var withActivity = history.Where(h => h.Income > 0 || h.Expense > 0).ToList();

        if (withActivity.Count == 0)
        {
            return new FinanceForecast(
                currentMonth.AddMonths(1), 0m, 0m, 0m, 0.1m,
                "No completed month of cash history yet, so there is nothing to project from.",
                history);
        }

        var predictedIncome = Project(history, h => h.Income);
        var predictedExpense = Project(history, h => h.Expense);

        // More complete months of real activity means a more trustworthy projection.
        var confidence = Math.Round(Math.Clamp(0.25m + 0.12m * withActivity.Count, 0.25m, 0.85m), 2);

        var rationale =
            $"Based on {withActivity.Count} month(s) of completed cash history. " +
            $"Recent average income {withActivity.Average(h => h.Income):N0}, " +
            $"expense {withActivity.Average(h => h.Expense):N0}. " +
            "The figure blends the linear trend with a 3-month moving average.";

        return new FinanceForecast(
            currentMonth.AddMonths(1),
            predictedIncome,
            predictedExpense,
            predictedIncome - predictedExpense,
            confidence,
            rationale,
            history);
    }

    private static decimal Project(
        IReadOnlyList<MonthlyFinancePoint> history, Func<MonthlyFinancePoint, decimal> selector)
    {
        var series = history.Select(h => (double)selector(h)).ToList();
        var movingAverage = Statistics.MovingAverage(series, 3);

        var points = series.Select((value, index) => ((double)index, value)).ToList();
        var fit = Statistics.Fit(points);

        if (fit is not { } line)
            return Math.Round((decimal)movingAverage, 2);

        var trendValue = line.PredictAt(series.Count);

        // Weight the trend by how well it actually explains the data; otherwise lean on the average.
        var weight = Math.Clamp(line.RSquared, 0d, 1d);
        var blended = trendValue * weight + movingAverage * (1 - weight);

        return Math.Round((decimal)Constrain(blended, movingAverage), 2);
    }

    /// <summary>
    /// Keeps a projection within a sensible distance of recent reality.
    ///
    /// A straight line fitted to a handful of months extrapolates without limit: a few declining
    /// months project through zero and into negative territory, which would have the farm predicted
    /// to earn nothing at all while still paying for feed. The trend is allowed to set the
    /// direction, but not to leave the neighbourhood of what the farm has actually been doing.
    /// </summary>
    private static double Constrain(double projected, double recentAverage)
    {
        // With no recent activity there is no neighbourhood to stay in; zero is the honest answer.
        if (recentAverage <= 0) return Math.Max(0d, projected);

        const double floorFactor = 0.4d;
        const double ceilingFactor = 2.0d;

        return Math.Clamp(projected, recentAverage * floorFactor, recentAverage * ceilingFactor);
    }
}
