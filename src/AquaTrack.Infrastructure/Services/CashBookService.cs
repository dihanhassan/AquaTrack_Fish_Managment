using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

public class CashBookService(AquaTrackDbContext db, ISettingsService settings) : ICashBookService
{
    public async Task<CashTransaction> PostAsync(
        DateOnly date,
        CashDirection direction,
        decimal amount,
        string category,
        PaymentMethod method = PaymentMethod.Cash,
        string? description = null,
        ReferenceType referenceType = ReferenceType.Manual,
        int? referenceId = null,
        int? customerId = null,
        int? supplierId = null,
        bool systemGenerated = true,
        CancellationToken ct = default)
    {
        var tx = new CashTransaction
        {
            TransactionDate = date,
            Direction = direction,
            Amount = Math.Abs(amount),
            Category = category,
            PaymentMethod = method,
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CustomerId = customerId,
            SupplierId = supplierId,
            IsSystemGenerated = systemGenerated,
        };

        db.CashTransactions.Add(tx);
        await db.SaveChangesAsync(ct);
        return tx;
    }

    public async Task ReverseAsync(ReferenceType referenceType, int referenceId, CancellationToken ct = default)
    {
        var rows = await db.CashTransactions
            .Where(t => t.ReferenceType == referenceType && t.ReferenceId == referenceId)
            .ToListAsync(ct);

        if (rows.Count == 0) return;

        db.CashTransactions.RemoveRange(rows); // Soft-deleted by the context.
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetOpeningBalanceAsync(DateOnly date, CancellationToken ct = default)
    {
        var configured = await settings.GetDecimalAsync(SettingKeys.OpeningCashBalance, 0m, ct);

        var movement = await db.CashTransactions
            .Where(t => t.TransactionDate < date)
            .SumAsync(t => t.Direction == CashDirection.In ? t.Amount : -t.Amount, ct);

        return configured + movement;
    }

    public async Task<CashSummary> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var opening = await GetOpeningBalanceAsync(from, ct);

        var window = db.CashTransactions.Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

        var totalIn = await window.Where(t => t.Direction == CashDirection.In).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var totalOut = await window.Where(t => t.Direction == CashDirection.Out).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        return new CashSummary(opening, totalIn, totalOut, opening + totalIn - totalOut);
    }

    public async Task<IReadOnlyList<CashLedgerRow>> GetLedgerAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var running = await GetOpeningBalanceAsync(from, ct);

        var rows = await db.CashTransactions
            .Include(t => t.Customer)
            .Include(t => t.Supplier)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var ledger = new List<CashLedgerRow>(rows.Count);
        foreach (var row in rows)
        {
            running += row.SignedAmount;
            ledger.Add(new CashLedgerRow(row, running));
        }

        return ledger;
    }
}
