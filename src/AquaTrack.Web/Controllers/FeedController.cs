using System.Linq.Expressions;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Common.Models;
using AquaTrack.Application.Services;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Feed inventory and its two movements. Purchases raise stock and roll the weighted-average cost
/// forward; usages issue stock to a pond and are costed at the average in force at that moment.
/// </summary>
public class FeedController(
    IApplicationDbContext db,
    IStockService stock,
    ICashBookService cashBook) : AppController
{
    private static readonly Dictionary<string, Expression<Func<FeedItem, object>>> Sortable = new()
    {
        ["name"] = f => f.Name,
        ["stock"] = f => f.CurrentStock,
        ["cost"] = f => f.AverageUnitCost,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Feed";
        ViewData["Subtitle"] = "Feed stock, purchases and pond usage";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<FeedListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var items = db.FeedItems.Include(f => f.Supplier).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(f => f.Name.Contains(term) || (f.Brand != null && f.Brand.Contains(term)));
        }

        if (Request.Query["low"] == "true")
            items = items.Where(f => f.ReorderLevel > 0 && f.CurrentStock <= f.ReorderLevel);

        var sorted = items.ApplySort(query.Sort, query.Desc, Sortable, "name");

        return new FeedListViewModel
        {
            Page = await PagedList<FeedItem>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            OnlyLowStock = Request.Query["low"] == "true",
        };
    }

    // ----- Feed item ----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        var model = new FeedItemFormViewModel();

        if (id is { } itemId)
        {
            var item = await db.FeedItems.AsNoTracking().FirstOrDefaultAsync(f => f.Id == itemId, ct);
            if (item is null) return NotFound();

            model = new FeedItemFormViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Brand = item.Brand,
                FeedType = item.FeedType,
                Unit = item.Unit,
                SupplierId = item.SupplierId,
                ReorderLevel = item.ReorderLevel,
                ProteinPercent = item.ProteinPercent,
                IsActive = item.IsActive,
            };
        }

        model.Suppliers = await SupplierOptionsAsync(ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FeedItemFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Suppliers = await SupplierOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        FeedItem item;
        if (model.Id is { } id)
        {
            item = await db.FeedItems.FirstOrDefaultAsync(f => f.Id == id, ct)
                   ?? throw new InvalidOperationException($"Feed item {id} not found.");
        }
        else
        {
            item = new FeedItem();
            db.FeedItems.Add(item);
        }

        item.Name = model.Name.Trim();
        item.Brand = model.Brand;
        item.FeedType = model.FeedType;
        item.Unit = model.Unit;
        item.SupplierId = model.SupplierId == 0 ? null : model.SupplierId;
        item.ReorderLevel = model.ReorderLevel;
        item.ProteinPercent = model.ProteinPercent;
        item.IsActive = model.IsActive;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"Feed item \"{item.Name}\" added." : "Feed item updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var item = await db.FeedItems.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (item is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete feed item",
            Message = $"Delete \"{item.Name}\"?",
            Detail = item.CurrentStock > 0
                ? $"There is still {item.CurrentStock:N2} in stock."
                : "Purchase and usage history is kept for reporting.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await db.FeedItems.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (item is null) return NotFound();

        if (item.CurrentStock > 0)
            return Failed("Issue or write off the remaining stock before deleting this item.");

        db.FeedItems.Remove(item);
        await db.SaveChangesAsync(ct);

        return Saved($"Feed item \"{item.Name}\" deleted.");
    }

    // ----- Purchase -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> PurchaseForm(int? feedItemId, CancellationToken ct)
        => PartialView("_PurchaseForm", new FeedPurchaseFormViewModel
        {
            FeedItemId = feedItemId ?? 0,
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            FeedItems = await FeedOptionsAsync(ct),
            Suppliers = await SupplierOptionsAsync(ct),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePurchase(FeedPurchaseFormViewModel model, CancellationToken ct)
    {
        if (model.PaidAmount > model.TotalPrice)
            ModelState.AddModelError(nameof(model.PaidAmount), "Paid amount cannot exceed the total.");

        if (!ModelState.IsValid)
        {
            model.FeedItems = await FeedOptionsAsync(ct);
            model.Suppliers = await SupplierOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_PurchaseForm", model);
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var purchase = new FeedPurchase
        {
            FeedItemId = model.FeedItemId,
            SupplierId = model.SupplierId == 0 ? null : model.SupplierId,
            PurchaseDate = model.PurchaseDate,
            Quantity = model.Quantity,
            UnitPrice = model.UnitPrice,
            TotalPrice = model.TotalPrice,
            PaymentMethod = model.PaymentMethod,
            PaidAmount = model.PaidAmount,
            InvoiceNo = model.InvoiceNo,
            Notes = model.Notes,
        };

        db.FeedPurchases.Add(purchase);
        await db.SaveChangesAsync(ct);

        // Raises stock and rolls the weighted-average cost forward.
        await stock.ReceiveFeedAsync(model.FeedItemId, model.Quantity, model.UnitPrice, ct);

        var category = await db.ExpenseCategories
            .FirstAsync(c => c.Name == ExpenseCategoryNames.Feed, ct);

        db.Expenses.Add(new Expense
        {
            ExpenseCategoryId = category.Id,
            ExpenseDate = model.PurchaseDate,
            Amount = model.TotalPrice,
            PaymentMethod = model.PaymentMethod,
            Description = $"Feed purchase: {model.Quantity:N2} units",
            VoucherNo = model.InvoiceNo,
            ReferenceType = ReferenceType.FeedPurchase,
            ReferenceId = purchase.Id,
            IsSystemGenerated = true,
        });

        // Unpaid balance becomes a supplier payable rather than leaving the cash book.
        if (purchase.SupplierId is { } supplierId && purchase.DueAmount > 0)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
            if (supplier is not null) supplier.PayableBalance += purchase.DueAmount;
        }

        await db.SaveChangesAsync(ct);

        if (model.PaidAmount > 0)
        {
            await cashBook.PostAsync(
                model.PurchaseDate, CashDirection.Out, model.PaidAmount,
                ExpenseCategoryNames.Feed, model.PaymentMethod,
                $"Feed purchase {model.InvoiceNo}".Trim(),
                ReferenceType.FeedPurchase, purchase.Id,
                supplierId: purchase.SupplierId, ct: ct);
        }

        await transaction.CommitAsync(ct);
        return Saved("Feed purchase recorded.");
    }

    // ----- Usage --------------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.RecordDailyData)]
    public async Task<IActionResult> UsageForm(int? feedItemId, CancellationToken ct)
        => PartialView("_UsageForm", new FeedUsageFormViewModel
        {
            FeedItemId = feedItemId ?? 0,
            UsageDate = DateOnly.FromDateTime(DateTime.Today),
            FeedItems = await FeedOptionsAsync(ct),
            Ponds = await PondOptionsAsync(ct),
        });

    [HttpPost]
    [Authorize(Policy = Policies.RecordDailyData)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUsage(FeedUsageFormViewModel model, CancellationToken ct)
    {
        var item = await db.FeedItems.AsNoTracking().FirstOrDefaultAsync(f => f.Id == model.FeedItemId, ct);

        if (item is not null && model.Quantity > item.CurrentStock)
            ModelState.AddModelError(nameof(model.Quantity),
                $"Only {item.CurrentStock:N2} {item.Unit} of {item.Name} is in stock.");

        if (!ModelState.IsValid)
        {
            model.FeedItems = await FeedOptionsAsync(ct);
            model.Ponds = await PondOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_UsageForm", model);
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        // Issuing returns the average cost at this moment, which is snapshotted on the usage row.
        var unitCost = await stock.IssueFeedAsync(model.FeedItemId, model.Quantity, ct);

        db.FeedUsages.Add(new FeedUsage
        {
            FeedItemId = model.FeedItemId,
            PondId = model.PondId,
            UsageDate = model.UsageDate,
            Quantity = model.Quantity,
            UnitCost = unitCost,
            TotalCost = Math.Round(model.Quantity * unitCost, 2),
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Saved("Feed usage recorded.");
    }

    // ----- Detail -------------------------------------------------------------------------

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var item = await db.FeedItems.Include(f => f.Supplier)
            .AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

        if (item is null) return NotFound();

        var model = new FeedDetailsViewModel
        {
            Item = item,
            Purchases = await db.FeedPurchases.Include(p => p.Supplier)
                .Where(p => p.FeedItemId == id)
                .OrderByDescending(p => p.PurchaseDate).Take(25)
                .AsNoTracking().ToListAsync(ct),
            Usages = await db.FeedUsages.Include(u => u.Pond)
                .Where(u => u.FeedItemId == id)
                .OrderByDescending(u => u.UsageDate).Take(25)
                .AsNoTracking().ToListAsync(ct),
        };

        ViewData["Title"] = item.Name;
        ViewData["Subtitle"] = $"{item.CurrentStock:N2} {item.Unit} in stock";
        return View(model);
    }

    // ----- Helpers ------------------------------------------------------------------------

    private async Task<IReadOnlyList<SelectOption>> SupplierOptionsAsync(CancellationToken ct)
        => await db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new SelectOption(s.Id, s.Name)).ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOption>> FeedOptionsAsync(CancellationToken ct)
        => await db.FeedItems.AsNoTracking().Where(f => f.IsActive)
            .OrderBy(f => f.Name).Select(f => new SelectOption(f.Id, f.Name)).ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOption>> PondOptionsAsync(CancellationToken ct)
        => await db.Ponds.AsNoTracking().Where(p => p.Status == PondStatus.Active)
            .OrderBy(p => p.Name).Select(p => new SelectOption(p.Id, p.Name)).ToListAsync(ct);
}
