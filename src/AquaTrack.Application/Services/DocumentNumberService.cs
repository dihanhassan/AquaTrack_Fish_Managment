using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

public interface IDocumentNumberService
{
    Task<string> NextSaleInvoiceAsync(DateOnly date, CancellationToken ct = default);
    Task<string> NextPurchaseNoAsync(DateOnly date, CancellationToken ct = default);
    Task<string> NextVoucherNoAsync(DateOnly date, CancellationToken ct = default);
}

/// <summary>
/// Produces human-readable document numbers such as <c>INV-202610-0007</c>. The sequence restarts each
/// month and is derived from the highest existing number, which keeps it correct after data imports.
/// </summary>
public class DocumentNumberService(IApplicationDbContext db) : IDocumentNumberService
{
    public Task<string> NextSaleInvoiceAsync(DateOnly date, CancellationToken ct = default)
        => NextAsync("INV", date, db.Sales.IgnoreQueryFilters().Select(s => s.InvoiceNo), ct);

    public Task<string> NextPurchaseNoAsync(DateOnly date, CancellationToken ct = default)
        => NextAsync("PUR", date, db.Purchases.IgnoreQueryFilters().Select(p => p.PurchaseNo), ct);

    public Task<string> NextVoucherNoAsync(DateOnly date, CancellationToken ct = default)
        => NextAsync("VCH", date, db.CashTransactions.IgnoreQueryFilters().Select(t => t.VoucherNo!), ct);

    private static async Task<string> NextAsync(
        string prefix, DateOnly date, IQueryable<string> existing, CancellationToken ct)
    {
        var period = $"{prefix}-{date:yyyyMM}-";

        var last = await existing
            .Where(n => n != null && n.StartsWith(period))
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct);

        var sequence = 1;
        if (last is not null && int.TryParse(last[period.Length..], out var previous))
            sequence = previous + 1;

        return $"{period}{sequence:D4}";
    }
}
