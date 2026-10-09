using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

public interface IPurchaseService
{
    /// <summary>
    /// Saves a purchase and everything that follows from it: inventory receipts, the expense line,
    /// the cash-book entry and the supplier's payable balance. All or nothing.
    /// </summary>
    Task<Purchase> CreateAsync(Purchase purchase, CancellationToken ct = default);

    /// <summary>Soft-deletes a purchase and reverses its stock, expense and cash effects.</summary>
    Task DeleteAsync(int purchaseId, CancellationToken ct = default);

    Task PayDueAsync(int purchaseId, decimal amount, PaymentMethod method, CancellationToken ct = default);
}

public class PurchaseService(
    IApplicationDbContext db,
    IStockService stock,
    ICashBookService cashBook,
    IDocumentNumberService numbers) : IPurchaseService
{
    public async Task<Purchase> CreateAsync(Purchase purchase, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        if (string.IsNullOrWhiteSpace(purchase.PurchaseNo))
            purchase.PurchaseNo = await numbers.NextPurchaseNoAsync(purchase.PurchaseDate, ct);

        RecalculateTotals(purchase);

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct);

        foreach (var item in purchase.Items)
            await ReceiveIntoInventoryAsync(item, ct);

        await PostFinancialsAsync(purchase, ct);

        await transaction.CommitAsync(ct);
        return purchase;
    }

    public async Task DeleteAsync(int purchaseId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var purchase = await db.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId, ct)
            ?? throw new InvalidOperationException($"Purchase {purchaseId} not found.");

        // Take the received quantities back out of stock before the document disappears.
        foreach (var item in purchase.Items)
            await ReverseInventoryAsync(item, ct);

        await cashBook.ReverseAsync(ReferenceType.Purchase, purchaseId, ct);

        var expenses = await db.Expenses
            .Where(e => e.ReferenceType == ReferenceType.Purchase && e.ReferenceId == purchaseId)
            .ToListAsync(ct);
        db.Expenses.RemoveRange(expenses);

        if (purchase.SupplierId is { } supplierId && purchase.DueAmount > 0)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
            if (supplier is not null)
                supplier.PayableBalance -= purchase.DueAmount;
        }

        db.Purchases.Remove(purchase);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    public async Task PayDueAsync(int purchaseId, decimal amount, PaymentMethod method, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct)
                       ?? throw new InvalidOperationException($"Purchase {purchaseId} not found.");

        var payable = Math.Min(amount, purchase.DueAmount);
        if (payable <= 0) return;

        purchase.PaidAmount += payable;

        if (purchase.SupplierId is { } supplierId)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
            if (supplier is not null)
                supplier.PayableBalance -= payable;
        }

        await db.SaveChangesAsync(ct);

        await cashBook.PostAsync(
            DateOnly.FromDateTime(DateTime.Today), CashDirection.Out, payable,
            "Supplier payment", method,
            $"Payment against purchase {purchase.PurchaseNo}",
            ReferenceType.SupplierPayment, purchase.Id,
            supplierId: purchase.SupplierId, ct: ct);

        await transaction.CommitAsync(ct);
    }

    internal static void RecalculateTotals(Purchase purchase)
    {
        foreach (var item in purchase.Items)
            item.LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);

        purchase.SubTotal = purchase.Items.Sum(i => i.LineTotal);
        purchase.GrandTotal = purchase.SubTotal - purchase.Discount + purchase.TransportCost;

        if (purchase.PaymentMethod != PaymentMethod.Credit && purchase.PaidAmount == 0)
            purchase.PaidAmount = purchase.GrandTotal;

        purchase.PaidAmount = Math.Min(purchase.PaidAmount, purchase.GrandTotal);
    }

    private async Task ReceiveIntoInventoryAsync(PurchaseItem item, CancellationToken ct)
    {
        switch (item.ItemType)
        {
            case PurchaseItemType.Feed when item.FeedItemId is { } feedId:
                await stock.ReceiveFeedAsync(feedId, item.Quantity, item.UnitPrice, ct);
                break;

            case PurchaseItemType.Medicine when item.MedicineId is { } medicineId:
                await stock.ReceiveMedicineAsync(medicineId, item.Quantity, item.UnitPrice, ct);
                break;

            case PurchaseItemType.EquipmentPart when item.EquipmentId is { } equipmentId:
                await ReceiveSparePartAsync(equipmentId, item, ct);
                break;

            // Equipment, fish fry and "other" lines carry no running stock of their own:
            // equipment is created from its own screen and fry become a FishStock batch.
        }
    }

    private async Task ReceiveSparePartAsync(int equipmentId, PurchaseItem item, CancellationToken ct)
    {
        var part = await db.EquipmentParts
            .FirstOrDefaultAsync(p => p.EquipmentId == equipmentId && p.Name == item.Description, ct);

        if (part is null)
        {
            db.EquipmentParts.Add(new EquipmentPart
            {
                EquipmentId = equipmentId,
                Name = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            });
        }
        else
        {
            part.UnitPrice = StockService.WeightedAverage(part.Quantity, part.UnitPrice, item.Quantity, item.UnitPrice);
            part.Quantity += item.Quantity;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ReverseInventoryAsync(PurchaseItem item, CancellationToken ct)
    {
        switch (item.ItemType)
        {
            case PurchaseItemType.Feed when item.FeedItemId is { } feedId:
            {
                var feed = await db.FeedItems.FirstOrDefaultAsync(f => f.Id == feedId, ct);
                if (feed is not null) feed.CurrentStock = Math.Max(0, feed.CurrentStock - item.Quantity);
                break;
            }

            case PurchaseItemType.Medicine when item.MedicineId is { } medicineId:
            {
                var medicine = await db.Medicines.FirstOrDefaultAsync(m => m.Id == medicineId, ct);
                if (medicine is not null) medicine.CurrentStock = Math.Max(0, medicine.CurrentStock - item.Quantity);
                break;
            }

            case PurchaseItemType.EquipmentPart when item.EquipmentId is { } equipmentId:
            {
                var part = await db.EquipmentParts
                    .FirstOrDefaultAsync(p => p.EquipmentId == equipmentId && p.Name == item.Description, ct);
                if (part is not null) part.Quantity = Math.Max(0, part.Quantity - item.Quantity);
                break;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Writes the expense line, the cash movement and any supplier credit the purchase creates.</summary>
    private async Task PostFinancialsAsync(Purchase purchase, CancellationToken ct)
    {
        var categoryName = DominantCategory(purchase);
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == categoryName, ct)
                       ?? await db.ExpenseCategories.FirstAsync(c => c.Name == ExpenseCategoryNames.Other, ct);

        db.Expenses.Add(new Expense
        {
            ExpenseCategoryId = category.Id,
            ExpenseDate = purchase.PurchaseDate,
            Amount = purchase.GrandTotal,
            PaymentMethod = purchase.PaymentMethod,
            Description = $"Purchase {purchase.PurchaseNo}",
            VoucherNo = purchase.PurchaseNo,
            ReferenceType = ReferenceType.Purchase,
            ReferenceId = purchase.Id,
            IsSystemGenerated = true,
        });

        if (purchase.SupplierId is { } supplierId && purchase.DueAmount > 0)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
            if (supplier is not null)
                supplier.PayableBalance += purchase.DueAmount;
        }

        await db.SaveChangesAsync(ct);

        // Only the amount actually paid leaves the cash book; the rest sits as a payable.
        if (purchase.PaidAmount > 0)
        {
            await cashBook.PostAsync(
                purchase.PurchaseDate, CashDirection.Out, purchase.PaidAmount,
                categoryName, purchase.PaymentMethod,
                $"Purchase {purchase.PurchaseNo}",
                ReferenceType.Purchase, purchase.Id,
                supplierId: purchase.SupplierId, ct: ct);
        }
    }

    /// <summary>Attributes the whole document to whichever item type accounts for most of its value.</summary>
    private static string DominantCategory(Purchase purchase)
    {
        if (purchase.Items.Count == 0) return ExpenseCategoryNames.Other;

        var topType = purchase.Items
            .GroupBy(i => i.ItemType)
            .OrderByDescending(g => g.Sum(i => i.LineTotal))
            .First().Key;

        return topType switch
        {
            PurchaseItemType.Feed => ExpenseCategoryNames.Feed,
            PurchaseItemType.Medicine => ExpenseCategoryNames.Medicine,
            PurchaseItemType.FishFry => ExpenseCategoryNames.FishFry,
            PurchaseItemType.Equipment or PurchaseItemType.EquipmentPart => ExpenseCategoryNames.Equipment,
            _ => ExpenseCategoryNames.Other,
        };
    }
}
