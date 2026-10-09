using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

public record DashboardSummary
{
    public int TotalFishCount { get; init; }
    public decimal TotalBiomassKg { get; init; }
    public int ActivePonds { get; init; }
    public int ActiveBatches { get; init; }

    public decimal FeedStockKg { get; init; }
    public decimal FeedStockValue { get; init; }
    public decimal MedicineStockValue { get; init; }

    public decimal TodayIncome { get; init; }
    public decimal TodayExpense { get; init; }
    public decimal TodayProfit => TodayIncome - TodayExpense;

    public decimal MonthIncome { get; init; }
    public decimal MonthExpense { get; init; }
    public decimal MonthProfit => MonthIncome - MonthExpense;

    public decimal CashBalance { get; init; }
    public decimal TotalProductionKg { get; init; }
    public decimal ReceivableTotal { get; init; }
    public decimal PayableTotal { get; init; }

    public int LowStockCount { get; init; }
    public int ExpiringMedicineCount { get; init; }
    public int UnreadNotifications { get; init; }
}

public record SeriesPoint(string Label, decimal Value);

public record DualSeriesPoint(string Label, decimal First, decimal Second);

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(DateOnly today, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesPoint>> GetDailySalesAsync(int days, CancellationToken ct = default);
    Task<IReadOnlyList<DualSeriesPoint>> GetIncomeVsExpenseAsync(int months, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesPoint>> GetExpenseByCategoryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesPoint>> GetFeedUsageByPondAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesPoint>> GetProductionByPondAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<SeriesPoint>> GetFishGrowthTrendAsync(int fishStockId, CancellationToken ct = default);
}

public class DashboardService(AquaTrackDbContext db, ICashBookService cashBook) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(DateOnly today, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var activeStocks = db.FishStocks.Where(s => s.Status == StockStatus.Active);

        var totalFish = await activeStocks.SumAsync(s => (int?)s.CurrentQuantity, ct) ?? 0;
        var biomassGram = await activeStocks
            .SumAsync(s => (decimal?)(s.CurrentQuantity * s.CurrentAvgWeightGram), ct) ?? 0m;

        var todayCash = await cashBook.GetSummaryAsync(today, today, ct);
        var monthCash = await cashBook.GetSummaryAsync(monthStart, today, ct);

        var feedItems = db.FeedItems.Where(f => f.IsActive);
        var medicines = db.Medicines.Where(m => m.IsActive);
        var expiryCutoff = today.AddDays(30);

        return new DashboardSummary
        {
            TotalFishCount = totalFish,
            TotalBiomassKg = Math.Round(biomassGram / 1000m, 2),
            ActivePonds = await db.Ponds.CountAsync(p => p.Status == PondStatus.Active, ct),
            ActiveBatches = await activeStocks.CountAsync(ct),

            FeedStockKg = await feedItems.SumAsync(f => (decimal?)f.CurrentStock, ct) ?? 0m,
            FeedStockValue = await feedItems.SumAsync(f => (decimal?)(f.CurrentStock * f.AverageUnitCost), ct) ?? 0m,
            MedicineStockValue = await medicines.SumAsync(m => (decimal?)(m.CurrentStock * m.AverageUnitCost), ct) ?? 0m,

            TodayIncome = todayCash.TotalIn,
            TodayExpense = todayCash.TotalOut,
            MonthIncome = monthCash.TotalIn,
            MonthExpense = monthCash.TotalOut,
            CashBalance = monthCash.ClosingBalance,

            TotalProductionKg = await db.Harvests.SumAsync(h => (decimal?)h.TotalWeightKg, ct) ?? 0m,
            ReceivableTotal = await db.Customers.SumAsync(c => (decimal?)c.ReceivableBalance, ct) ?? 0m,
            PayableTotal = await db.Suppliers.SumAsync(s => (decimal?)s.PayableBalance, ct) ?? 0m,

            LowStockCount = await feedItems.CountAsync(f => f.ReorderLevel > 0 && f.CurrentStock <= f.ReorderLevel, ct)
                          + await medicines.CountAsync(m => m.ReorderLevel > 0 && m.CurrentStock <= m.ReorderLevel, ct),
            ExpiringMedicineCount = await medicines.CountAsync(
                m => m.ExpiryDate != null && m.ExpiryDate <= expiryCutoff, ct),
            UnreadNotifications = await db.Notifications.CountAsync(n => !n.IsRead, ct),
        };
    }

    public async Task<IReadOnlyList<SeriesPoint>> GetDailySalesAsync(int days, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-(days - 1));

        var totals = await db.Sales
            .Where(s => s.SaleDate >= from && s.SaleDate <= today)
            .GroupBy(s => s.SaleDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(s => s.GrandTotal) })
            .ToDictionaryAsync(x => x.Date, x => x.Total, ct);

        // Days with no sales are filled with zero so the chart keeps one point per day.
        return [.. Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new SeriesPoint(date.ToString("dd MMM"), totals.GetValueOrDefault(date)))];
    }

    public async Task<IReadOnlyList<DualSeriesPoint>> GetIncomeVsExpenseAsync(int months, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var rows = await db.CashTransactions
            .Where(t => t.TransactionDate >= from)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Direction })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Direction, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return [.. Enumerable.Range(0, months)
            .Select(offset => from.AddMonths(offset))
            .Select(month => new DualSeriesPoint(
                month.ToString("MMM yy"),
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Direction == CashDirection.In)
                    .Sum(r => r.Total),
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Direction == CashDirection.Out)
                    .Sum(r => r.Total)))];
    }

    public async Task<IReadOnlyList<SeriesPoint>> GetExpenseByCategoryAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await db.Expenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(e => e.Amount)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SeriesPoint>> GetFeedUsageByPondAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await db.FeedUsages
            .Where(u => u.UsageDate >= from && u.UsageDate <= to)
            .GroupBy(u => u.Pond!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(u => u.Quantity)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SeriesPoint>> GetProductionByPondAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await db.Harvests
            .Where(h => h.HarvestDate >= from && h.HarvestDate <= to)
            .GroupBy(h => h.FishStock!.Pond!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(h => h.TotalWeightKg)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SeriesPoint>> GetFishGrowthTrendAsync(
        int fishStockId, CancellationToken ct = default)
        => await db.FishGrowths
            .Where(g => g.FishStockId == fishStockId)
            .OrderBy(g => g.RecordDate)
            .Select(g => new SeriesPoint(g.RecordDate.ToString("dd MMM"), g.AvgWeightGram))
            .ToListAsync(ct);
}
