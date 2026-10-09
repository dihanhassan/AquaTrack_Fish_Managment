using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

public interface IStockService
{
    /// <summary>Recomputes a batch's live quantity and average weight from its movement history.</summary>
    Task RecalculateFishStockAsync(int fishStockId, CancellationToken ct = default);

    /// <summary>Adds purchased quantity and rolls the weighted-average cost forward.</summary>
    Task ReceiveFeedAsync(int feedItemId, decimal quantity, decimal unitPrice, CancellationToken ct = default);
    Task ReceiveMedicineAsync(int medicineId, decimal quantity, decimal unitPrice, CancellationToken ct = default);

    /// <summary>Issues stock to a pond, returning the costed quantity. Throws if stock is insufficient.</summary>
    Task<decimal> IssueFeedAsync(int feedItemId, decimal quantity, CancellationToken ct = default);
    Task<decimal> IssueMedicineAsync(int medicineId, decimal quantity, CancellationToken ct = default);
}

public class InsufficientStockException(string itemName, decimal available, decimal requested)
    : InvalidOperationException($"Not enough {itemName} in stock: {available:N3} available, {requested:N3} requested.")
{
    public string ItemName { get; } = itemName;
    public decimal Available { get; } = available;
    public decimal Requested { get; } = requested;
}

public class StockService(IApplicationDbContext db) : IStockService
{
    public async Task RecalculateFishStockAsync(int fishStockId, CancellationToken ct = default)
    {
        var stock = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == fishStockId, ct);
        if (stock is null) return;

        var dead = await db.FishMortalities.Where(m => m.FishStockId == fishStockId)
            .SumAsync(m => (int?)m.Quantity, ct) ?? 0;
        var harvested = await db.Harvests.Where(h => h.FishStockId == fishStockId)
            .SumAsync(h => (int?)h.Quantity, ct) ?? 0;
        var adjusted = await db.StockAdjustments.Where(a => a.FishStockId == fishStockId)
            .SumAsync(a => (int?)a.QuantityDelta, ct) ?? 0;

        stock.CurrentQuantity = Math.Max(0, stock.InitialQuantity - dead - harvested + adjusted);

        // Kept separate so reports can tell a loss (died) from a sale (harvested).
        stock.MortalityCount = dead;
        stock.HarvestedCount = harvested;

        // Average weight tracks the most recent sampling; falls back to the stocking weight.
        var latestGrowth = await db.FishGrowths
            .Where(g => g.FishStockId == fishStockId)
            .OrderByDescending(g => g.RecordDate).ThenByDescending(g => g.Id)
            .FirstOrDefaultAsync(ct);

        stock.CurrentAvgWeightGram = latestGrowth?.AvgWeightGram ?? stock.InitialAvgWeightGram;

        if (stock.CurrentQuantity == 0 && stock.Status == StockStatus.Active)
            stock.Status = harvested > 0 ? StockStatus.FullyHarvested : StockStatus.Lost;

        await db.SaveChangesAsync(ct);
    }

    public async Task ReceiveFeedAsync(int feedItemId, decimal quantity, decimal unitPrice, CancellationToken ct = default)
    {
        var item = await db.FeedItems.FirstOrDefaultAsync(f => f.Id == feedItemId, ct)
                   ?? throw new InvalidOperationException($"Feed item {feedItemId} not found.");

        item.AverageUnitCost = WeightedAverage(item.CurrentStock, item.AverageUnitCost, quantity, unitPrice);
        item.CurrentStock += quantity;
        item.LastUnitPrice = unitPrice;

        await db.SaveChangesAsync(ct);
    }

    public async Task ReceiveMedicineAsync(int medicineId, decimal quantity, decimal unitPrice, CancellationToken ct = default)
    {
        var item = await db.Medicines.FirstOrDefaultAsync(m => m.Id == medicineId, ct)
                   ?? throw new InvalidOperationException($"Medicine {medicineId} not found.");

        item.AverageUnitCost = WeightedAverage(item.CurrentStock, item.AverageUnitCost, quantity, unitPrice);
        item.CurrentStock += quantity;
        item.LastUnitPrice = unitPrice;

        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> IssueFeedAsync(int feedItemId, decimal quantity, CancellationToken ct = default)
    {
        var item = await db.FeedItems.FirstOrDefaultAsync(f => f.Id == feedItemId, ct)
                   ?? throw new InvalidOperationException($"Feed item {feedItemId} not found.");

        if (item.CurrentStock < quantity)
            throw new InsufficientStockException(item.Name, item.CurrentStock, quantity);

        item.CurrentStock -= quantity;
        await db.SaveChangesAsync(ct);
        return item.AverageUnitCost;
    }

    public async Task<decimal> IssueMedicineAsync(int medicineId, decimal quantity, CancellationToken ct = default)
    {
        var item = await db.Medicines.FirstOrDefaultAsync(m => m.Id == medicineId, ct)
                   ?? throw new InvalidOperationException($"Medicine {medicineId} not found.");

        if (item.CurrentStock < quantity)
            throw new InsufficientStockException(item.Name, item.CurrentStock, quantity);

        item.CurrentStock -= quantity;
        await db.SaveChangesAsync(ct);
        return item.AverageUnitCost;
    }

    /// <summary>
    /// Moving weighted average: blends the value already on hand with the value being received.
    /// Falls back to the incoming price when there is nothing on hand to blend with.
    /// </summary>
    internal static decimal WeightedAverage(
        decimal currentQty, decimal currentCost, decimal incomingQty, decimal incomingCost)
    {
        var totalQty = currentQty + incomingQty;
        if (totalQty <= 0) return incomingCost;
        if (currentQty <= 0) return incomingCost;

        return Math.Round((currentQty * currentCost + incomingQty * incomingCost) / totalQty, 4);
    }
}
