using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

public interface ISaleService
{
    /// <summary>
    /// Saves a sale and everything that follows: harvest records against the batches sold from,
    /// the cash-book entry and the customer's receivable balance. All or nothing.
    /// </summary>
    Task<Sale> CreateAsync(Sale sale, bool deductFromStock = true, CancellationToken ct = default);

    Task DeleteAsync(int saleId, CancellationToken ct = default);

    Task ReceiveDueAsync(int saleId, decimal amount, PaymentMethod method, CancellationToken ct = default);
}

public class SaleService(
    AquaTrackDbContext db,
    ICashBookService cashBook,
    IStockService stock,
    IDocumentNumberService numbers) : ISaleService
{
    public async Task<Sale> CreateAsync(Sale sale, bool deductFromStock = true, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (string.IsNullOrWhiteSpace(sale.InvoiceNo))
            sale.InvoiceNo = await numbers.NextSaleInvoiceAsync(sale.SaleDate, ct);

        RecalculateTotals(sale);

        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);

        if (deductFromStock)
            await RecordHarvestsAsync(sale, ct);

        if (sale.CustomerId is { } customerId && sale.DueAmount > 0)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is not null)
            {
                customer.ReceivableBalance += sale.DueAmount;
                await db.SaveChangesAsync(ct);
            }
        }

        // Only the amount actually collected enters the cash book; the rest is a receivable.
        if (sale.PaidAmount > 0)
        {
            await cashBook.PostAsync(
                sale.SaleDate, CashDirection.In, sale.PaidAmount,
                "Fish sale", sale.PaymentMethod,
                $"Sale {sale.InvoiceNo}",
                ReferenceType.Sale, sale.Id,
                customerId: sale.CustomerId, ct: ct);
        }

        await transaction.CommitAsync(ct);
        return sale;
    }

    public async Task DeleteAsync(int saleId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var sale = await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            ?? throw new InvalidOperationException($"Sale {saleId} not found.");

        // Remove the harvest rows this sale created, then put the fish back on the batch.
        var harvests = await db.Harvests
            .Where(h => h.CreatedBy != null && h.Notes != null && h.Notes.Contains(sale.InvoiceNo))
            .ToListAsync(ct);

        var affectedStocks = harvests.Select(h => h.FishStockId).Distinct().ToList();
        db.Harvests.RemoveRange(harvests);

        await cashBook.ReverseAsync(ReferenceType.Sale, saleId, ct);

        if (sale.CustomerId is { } customerId && sale.DueAmount > 0)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is not null)
                customer.ReceivableBalance -= sale.DueAmount;
        }

        db.Sales.Remove(sale);
        await db.SaveChangesAsync(ct);

        foreach (var stockId in affectedStocks)
            await stock.RecalculateFishStockAsync(stockId, ct);

        await transaction.CommitAsync(ct);
    }

    public async Task ReceiveDueAsync(int saleId, decimal amount, PaymentMethod method, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId, ct)
                   ?? throw new InvalidOperationException($"Sale {saleId} not found.");

        var receivable = Math.Min(amount, sale.DueAmount);
        if (receivable <= 0) return;

        sale.PaidAmount += receivable;

        if (sale.CustomerId is { } customerId)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is not null)
                customer.ReceivableBalance -= receivable;
        }

        await db.SaveChangesAsync(ct);

        await cashBook.PostAsync(
            DateOnly.FromDateTime(DateTime.Today), CashDirection.In, receivable,
            "Customer payment", method,
            $"Payment against invoice {sale.InvoiceNo}",
            ReferenceType.CustomerPayment, sale.Id,
            customerId: sale.CustomerId, ct: ct);

        await transaction.CommitAsync(ct);
    }

    internal static void RecalculateTotals(Sale sale)
    {
        foreach (var item in sale.Items)
            item.LineTotal = Math.Round(item.QuantityKg * item.UnitPrice, 2);

        sale.SubTotal = sale.Items.Sum(i => i.LineTotal);
        sale.GrandTotal = sale.SubTotal - sale.Discount;

        if (sale.PaymentMethod != PaymentMethod.Credit && sale.PaidAmount == 0)
            sale.PaidAmount = sale.GrandTotal;

        sale.PaidAmount = Math.Min(sale.PaidAmount, sale.GrandTotal);
    }

    /// <summary>
    /// A sale line that names a batch also takes those fish out of it. Piece count is estimated from
    /// the batch's current average weight when the user did not enter one.
    /// </summary>
    private async Task RecordHarvestsAsync(Sale sale, CancellationToken ct)
    {
        var touched = new List<int>();

        foreach (var item in sale.Items)
        {
            if (item.FishStockId is not { } stockId) continue;

            var batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == stockId, ct);
            if (batch is null) continue;

            var avgWeight = batch.CurrentAvgWeightGram > 0 ? batch.CurrentAvgWeightGram : batch.InitialAvgWeightGram;
            var pieces = item.PieceCount
                         ?? (avgWeight > 0 ? (int)Math.Round(item.QuantityKg * 1000m / avgWeight) : 0);

            pieces = Math.Min(pieces, batch.CurrentQuantity);

            db.Harvests.Add(new Harvest
            {
                FishStockId = stockId,
                HarvestDate = sale.SaleDate,
                Quantity = pieces,
                TotalWeightKg = item.QuantityKg,
                ExpectedUnitPrice = item.UnitPrice,
                IsPartial = true,
                Notes = $"Auto-recorded from sale {sale.InvoiceNo}",
            });

            touched.Add(stockId);
        }

        if (touched.Count == 0) return;

        await db.SaveChangesAsync(ct);

        foreach (var stockId in touched.Distinct())
            await stock.RecalculateFishStockAsync(stockId, ct);
    }
}
