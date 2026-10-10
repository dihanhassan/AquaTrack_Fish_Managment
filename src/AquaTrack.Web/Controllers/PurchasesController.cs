using System.Linq.Expressions;
using AquaTrack.Application.Common.Interfaces;
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
/// Purchase documents. A single purchase can mix feed, medicine, equipment and other costs;
/// <see cref="IPurchaseService"/> decides which inventory each line touches and posts the expense,
/// the cash movement and the supplier payable together.
/// </summary>
[Authorize(Policy = Policies.ManageFarm)]
public class PurchasesController(IApplicationDbContext db, IPurchaseService purchases) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Purchase, object>>> Sortable = new()
    {
        ["date"] = p => p.PurchaseDate,
        ["number"] = p => p.PurchaseNo,
        ["supplier"] = p => p.Supplier!.Name,
        ["total"] = p => p.GrandTotal,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Purchases";
        ViewData["Subtitle"] = "What the farm bought and what is still owed";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<PurchaseListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var documents = db.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            documents = documents.Where(p =>
                p.PurchaseNo.Contains(term) ||
                (p.InvoiceNo != null && p.InvoiceNo.Contains(term)) ||
                (p.Supplier != null && p.Supplier.Name.Contains(term)));
        }

        if (Request.Query["dues"] == "true")
            documents = documents.Where(p => p.PaidAmount < p.GrandTotal);

        var sorted = documents.ApplySort(query.Sort, query.Desc, Sortable, "date");
        if (string.IsNullOrWhiteSpace(query.Sort)) sorted = sorted.OrderByDescending(p => p.PurchaseDate);

        return new PurchaseListViewModel
        {
            Page = await PagedList<Purchase>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            OnlyWithDues = Request.Query["dues"] == "true",
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(CancellationToken ct)
    {
        var model = new PurchaseFormViewModel
        {
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            Items = [new PurchaseLineViewModel()],
        };

        await FillOptionsAsync(model, ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PurchaseFormViewModel model, CancellationToken ct)
    {
        var lines = model.Items.Where(i => i.IsFilled).ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one line with a description, quantity and price.");

        if (model.PurchaseDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.PurchaseDate), "A purchase cannot be dated in the future.");

        // A line that names an inventory type must point at an actual item, or nothing is received.
        foreach (var line in lines)
        {
            var needsTarget = line.ItemType is PurchaseItemType.Feed or PurchaseItemType.Medicine;
            if (needsTarget && line.TargetId <= 0)
                ModelState.AddModelError(string.Empty,
                    $"Choose which {line.ItemType.ToString().ToLowerInvariant()} the line refers to.");
        }

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        var purchase = new Purchase
        {
            SupplierId = model.SupplierId == 0 ? null : model.SupplierId,
            PurchaseDate = model.PurchaseDate,
            Discount = model.Discount,
            TransportCost = model.TransportCost,
            PaymentMethod = model.PaymentMethod,
            PaidAmount = model.PaidAmount,
            InvoiceNo = model.InvoiceNo,
            Notes = model.Notes,
            Items = [.. lines.Select(l => new PurchaseItem
            {
                ItemType = l.ItemType,
                FeedItemId = l.ItemType == PurchaseItemType.Feed ? l.TargetId : null,
                MedicineId = l.ItemType == PurchaseItemType.Medicine ? l.TargetId : null,
                EquipmentId = l.ItemType is PurchaseItemType.Equipment or PurchaseItemType.EquipmentPart
                    && l.TargetId > 0 ? l.TargetId : null,
                FishTypeId = l.ItemType == PurchaseItemType.FishFry && l.TargetId > 0 ? l.TargetId : null,
                Description = l.Description.Trim(),
                Unit = l.Unit,
                Quantity = l.Quantity!.Value,
                UnitPrice = l.UnitPrice!.Value,
            })],
        };

        await purchases.CreateAsync(purchase, ct);

        return Saved($"Purchase {purchase.PurchaseNo} recorded for {purchase.GrandTotal:N2}.");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var purchase = await db.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.FeedItem)
            .Include(p => p.Items).ThenInclude(i => i.Medicine)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (purchase is null) return NotFound();

        ViewData["Title"] = $"Purchase {purchase.PurchaseNo}";
        ViewData["Subtitle"] = purchase.PurchaseDate.ToString("dd MMM yyyy");
        return View(purchase);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentForm(int id, CancellationToken ct)
    {
        var purchase = await db.Purchases.Include(p => p.Supplier)
            .AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

        if (purchase is null) return NotFound();

        return PartialView("_PaymentForm", new PurchasePaymentViewModel
        {
            PurchaseId = purchase.Id,
            PurchaseNo = purchase.PurchaseNo,
            SupplierName = purchase.Supplier?.Name ?? "-",
            DueAmount = purchase.DueAmount,
            Amount = purchase.DueAmount,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayDue(PurchasePaymentViewModel model, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.PurchaseId, ct);

        if (purchase is null) return NotFound();

        if (model.Amount > purchase.DueAmount)
            ModelState.AddModelError(nameof(model.Amount),
                $"Only {purchase.DueAmount:N2} is outstanding on this purchase.");

        if (!ModelState.IsValid)
        {
            model.DueAmount = purchase.DueAmount;
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_PaymentForm", model);
        }

        await purchases.PayDueAsync(model.PurchaseId, model.Amount, model.PaymentMethod, ct);
        return Saved($"Paid {model.Amount:N2} against {purchase.PurchaseNo}.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete purchase",
            Message = $"Delete {purchase.PurchaseNo}?",
            Detail = "The stock it received is taken back out, and its expense, cash entry and supplier balance are reversed.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return NotFound();

        await purchases.DeleteAsync(id, ct);
        return Saved($"Purchase {purchase.PurchaseNo} deleted.");
    }

    /// <summary>
    /// The target dropdown for a line, which depends on the item type chosen: a feed line picks a
    /// feed item, a medicine line a medicine, and so on.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TargetOptions(PurchaseItemType itemType, int index, int? selected, CancellationToken ct)
    {
        IReadOnlyList<SelectOption> options = itemType switch
        {
            PurchaseItemType.Feed => await db.FeedItems.AsNoTracking().Where(f => f.IsActive)
                .OrderBy(f => f.Name).Select(f => new SelectOption(f.Id, f.Name)).ToListAsync(ct),

            PurchaseItemType.Medicine => await db.Medicines.AsNoTracking().Where(m => m.IsActive)
                .OrderBy(m => m.Name).Select(m => new SelectOption(m.Id, m.Name)).ToListAsync(ct),

            PurchaseItemType.Equipment or PurchaseItemType.EquipmentPart =>
                await db.Equipments.AsNoTracking()
                    .OrderBy(e => e.Name).Select(e => new SelectOption(e.Id, e.Name)).ToListAsync(ct),

            PurchaseItemType.FishFry => await db.FishTypes.AsNoTracking()
                .OrderBy(f => f.Name).Select(f => new SelectOption(f.Id, f.Name)).ToListAsync(ct),

            _ => [],
        };

        ViewData["Index"] = index;
        ViewData["Selected"] = selected;
        ViewData["Required"] = itemType is PurchaseItemType.Feed or PurchaseItemType.Medicine;
        return PartialView("_TargetOptions", options);
    }

    private async Task FillOptionsAsync(PurchaseFormViewModel model, CancellationToken ct)
        => model.Suppliers = await db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new SelectOption(s.Id, s.Name)).ToListAsync(ct);
}
