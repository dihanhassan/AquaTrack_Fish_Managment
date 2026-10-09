using AquaTrack.Domain.Entities;
using AquaTrack.Infrastructure.Services;

namespace AquaTrack.Web.Models;

public class DashboardViewModel
{
    public DateOnly Today { get; init; }
    public string CurrencySymbol { get; init; } = "Tk";

    public required DashboardSummary Summary { get; init; }

    public IReadOnlyList<SeriesPoint> DailySales { get; init; } = [];
    public IReadOnlyList<DualSeriesPoint> IncomeVsExpense { get; init; } = [];
    public IReadOnlyList<SeriesPoint> ExpenseByCategory { get; init; } = [];
    public IReadOnlyList<SeriesPoint> FeedUsageByPond { get; init; } = [];
    public IReadOnlyList<SeriesPoint> ProductionByPond { get; init; } = [];
    public IReadOnlyList<Notification> RecentAlerts { get; init; } = [];

    /// <summary>Formats an amount with the configured currency symbol, for display only.</summary>
    public string Money(decimal value) => $"{CurrencySymbol} {value:N0}";
}
