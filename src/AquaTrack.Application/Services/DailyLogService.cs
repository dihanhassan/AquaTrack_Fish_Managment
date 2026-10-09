using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

/// <summary>One screen's worth of daily input, before it is split into the records it creates.</summary>
public class DailyInput
{
    public DateOnly LogDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int PondId { get; set; }
    public string? Weather { get; set; }
    public string? Observations { get; set; }

    public List<DailyFeedLine> FeedLines { get; set; } = [];
    public List<DailyMedicineLine> MedicineLines { get; set; } = [];

    public int? MortalityCount { get; set; }
    public MortalityCause MortalityCause { get; set; } = MortalityCause.Unknown;
    public int? FishStockId { get; set; }

    public decimal? SampleAvgWeightGram { get; set; }
    public int? SampleSize { get; set; }

    public decimal? SaleQuantityKg { get; set; }
    public decimal? SaleUnitPrice { get; set; }
    public int? SaleFishTypeId { get; set; }
    public int? SaleCustomerId { get; set; }
    public PaymentMethod SalePaymentMethod { get; set; } = PaymentMethod.Cash;

    public decimal? OtherIncomeAmount { get; set; }
    public string? OtherIncomeSource { get; set; }

    public decimal? ExpenseAmount { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public string? ExpenseDescription { get; set; }
}

public class DailyFeedLine
{
    public int FeedItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class DailyMedicineLine
{
    public int MedicineId { get; set; }
    public decimal Quantity { get; set; }
    public string? Purpose { get; set; }
}

public interface IDailyLogService
{
    /// <summary>
    /// Writes one day of pond activity as a single transaction: feed and medicine issues, mortality,
    /// a growth sample, a sale, other income and an expense, each only if the user entered it.
    /// </summary>
    Task<DailyLog> SaveAsync(DailyInput input, CancellationToken ct = default);

    Task<DailyLog?> GetAsync(int pondId, DateOnly date, CancellationToken ct = default);
}

public class DailyLogService(
    IApplicationDbContext db,
    IStockService stock,
    ICashBookService cashBook,
    ISaleService sales) : IDailyLogService
{
    public Task<DailyLog?> GetAsync(int pondId, DateOnly date, CancellationToken ct = default)
        => db.DailyLogs.FirstOrDefaultAsync(d => d.PondId == pondId && d.LogDate == date, ct);

    public async Task<DailyLog> SaveAsync(DailyInput input, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        // One log per pond per day: re-opening the screen tops up the same log rather than duplicating it.
        var log = await GetAsync(input.PondId, input.LogDate, ct);
        if (log is null)
        {
            log = new DailyLog { PondId = input.PondId, LogDate = input.LogDate };
            db.DailyLogs.Add(log);
        }

        if (log.IsLocked)
            throw new InvalidOperationException("This day has been locked and can no longer be edited.");

        log.Weather = input.Weather;
        log.Observations = input.Observations;
        await db.SaveChangesAsync(ct);

        await ApplyFeedAsync(input, log, ct);
        await ApplyMedicineAsync(input, log, ct);
        await ApplyMortalityAsync(input, log, ct);
        await ApplyGrowthSampleAsync(input, log, ct);
        await ApplySaleAsync(input, log, ct);
        await ApplyOtherIncomeAsync(input, log, ct);
        await ApplyExpenseAsync(input, log, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return log;
    }

    private async Task ApplyFeedAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        foreach (var line in input.FeedLines.Where(l => l.FeedItemId > 0 && l.Quantity > 0))
        {
            var unitCost = await stock.IssueFeedAsync(line.FeedItemId, line.Quantity, ct);
            var totalCost = Math.Round(line.Quantity * unitCost, 2);

            db.FeedUsages.Add(new FeedUsage
            {
                FeedItemId = line.FeedItemId,
                PondId = input.PondId,
                UsageDate = input.LogDate,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                TotalCost = totalCost,
                DailyLogId = log.Id,
            });

            log.TotalFeedKg += line.Quantity;
            log.TotalFeedCost += totalCost;
        }
    }

    private async Task ApplyMedicineAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        foreach (var line in input.MedicineLines.Where(l => l.MedicineId > 0 && l.Quantity > 0))
        {
            var unitCost = await stock.IssueMedicineAsync(line.MedicineId, line.Quantity, ct);
            var totalCost = Math.Round(line.Quantity * unitCost, 2);

            db.MedicineUsages.Add(new MedicineUsage
            {
                MedicineId = line.MedicineId,
                PondId = input.PondId,
                UsageDate = input.LogDate,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                TotalCost = totalCost,
                Purpose = line.Purpose,
                DailyLogId = log.Id,
            });

            log.TotalMedicineCost += totalCost;
        }
    }

    private async Task ApplyMortalityAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        if (input.MortalityCount is not > 0) return;

        var stockId = input.FishStockId ?? await ResolveBatchAsync(input.PondId, ct);
        if (stockId is null) return;

        var batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == stockId, ct);
        var avgWeight = batch?.CurrentAvgWeightGram ?? 0m;

        db.FishMortalities.Add(new FishMortality
        {
            FishStockId = stockId.Value,
            RecordDate = input.LogDate,
            Quantity = input.MortalityCount.Value,
            Cause = input.MortalityCause,
            AvgWeightGram = avgWeight,
            EstimatedLoss = Math.Round(input.MortalityCount.Value * (batch?.UnitCost ?? 0m), 2),
            DailyLogId = log.Id,
        });

        log.TotalMortality += input.MortalityCount.Value;

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(stockId.Value, ct);
    }

    private async Task ApplyGrowthSampleAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        if (input.SampleAvgWeightGram is not > 0) return;

        var stockId = input.FishStockId ?? await ResolveBatchAsync(input.PondId, ct);
        if (stockId is null) return;

        db.FishGrowths.Add(new FishGrowth
        {
            FishStockId = stockId.Value,
            RecordDate = input.LogDate,
            SampleSize = input.SampleSize ?? 10,
            AvgWeightGram = input.SampleAvgWeightGram.Value,
            DailyLogId = log.Id,
        });

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(stockId.Value, ct);
    }

    private async Task ApplySaleAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        if (input.SaleQuantityKg is not > 0 || input.SaleUnitPrice is not > 0 || input.SaleFishTypeId is null)
            return;

        var sale = new Sale
        {
            SaleDate = input.LogDate,
            CustomerId = input.SaleCustomerId,
            PaymentMethod = input.SalePaymentMethod,
            DailyLogId = log.Id,
            Notes = $"Recorded from daily input for {input.LogDate:dd MMM yyyy}",
            Items =
            [
                new SaleItem
                {
                    FishTypeId = input.SaleFishTypeId.Value,
                    PondId = input.PondId,
                    FishStockId = input.FishStockId ?? await ResolveBatchAsync(input.PondId, ct),
                    QuantityKg = input.SaleQuantityKg.Value,
                    UnitPrice = input.SaleUnitPrice.Value,
                },
            ],
        };

        // The sale service owns invoice numbering, harvest write-back and the cash posting.
        await sales.CreateAsync(sale, deductFromStock: true, ct);
        log.TotalSales += sale.GrandTotal;
    }

    private async Task ApplyOtherIncomeAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        if (input.OtherIncomeAmount is not > 0) return;

        var income = new OtherIncome
        {
            IncomeDate = input.LogDate,
            Amount = input.OtherIncomeAmount.Value,
            Source = input.OtherIncomeSource ?? "Other income",
            PaymentMethod = PaymentMethod.Cash,
            DailyLogId = log.Id,
        };

        db.OtherIncomes.Add(income);
        await db.SaveChangesAsync(ct);

        await cashBook.PostAsync(
            input.LogDate, CashDirection.In, income.Amount,
            "Other income", PaymentMethod.Cash, income.Source,
            ReferenceType.OtherIncome, income.Id, ct: ct);

        log.TotalOtherIncome += income.Amount;
    }

    private async Task ApplyExpenseAsync(DailyInput input, DailyLog log, CancellationToken ct)
    {
        if (input.ExpenseAmount is not > 0) return;

        var categoryId = input.ExpenseCategoryId
                         ?? await db.ExpenseCategories
                             .Where(c => c.Name == ExpenseCategoryNames.Other)
                             .Select(c => c.Id)
                             .FirstAsync(ct);

        var expense = new Expense
        {
            ExpenseCategoryId = categoryId,
            ExpenseDate = input.LogDate,
            Amount = input.ExpenseAmount.Value,
            PaymentMethod = PaymentMethod.Cash,
            Description = input.ExpenseDescription,
            PondId = input.PondId,
            DailyLogId = log.Id,
            ReferenceType = ReferenceType.Expense,
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        var categoryName = await db.ExpenseCategories
            .Where(c => c.Id == categoryId)
            .Select(c => c.Name)
            .FirstAsync(ct);

        await cashBook.PostAsync(
            input.LogDate, CashDirection.Out, expense.Amount,
            categoryName, PaymentMethod.Cash, expense.Description,
            ReferenceType.Expense, expense.Id, ct: ct);

        log.TotalExpense += expense.Amount;
    }

    /// <summary>
    /// The daily screen is pond-first, so when the user does not pick a batch we take the pond's
    /// only active one. With several active batches we cannot guess, and the record is skipped.
    /// </summary>
    private async Task<int?> ResolveBatchAsync(int pondId, CancellationToken ct)
    {
        var active = await db.FishStocks
            .Where(s => s.PondId == pondId && s.Status == StockStatus.Active)
            .Select(s => s.Id)
            .Take(2)
            .ToListAsync(ct);

        return active.Count == 1 ? active[0] : null;
    }
}
