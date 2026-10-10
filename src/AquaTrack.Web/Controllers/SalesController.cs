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
/// Fish sales. The controller only shapes input; <see cref="ISaleService"/> owns the posting, so
/// an invoice, its harvest write-back, the cash entry and the customer balance always move together.
/// </summary>
[Authorize(Policy = Policies.ManageFarm)]
public class SalesController(IApplicationDbContext db, ISaleService sales) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Sale, object>>> Sortable = new()
    {
        ["date"] = s => s.SaleDate,
        ["invoice"] = s => s.InvoiceNo,
        ["customer"] = s => s.Customer!.Name,
        ["total"] = s => s.GrandTotal,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Sales";
        ViewData["Subtitle"] = "Fish sold and what is still owed";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<SaleListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var invoices = db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.FishType)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            invoices = invoices.Where(s =>
                s.InvoiceNo.Contains(term) ||
                (s.Customer != null && s.Customer.Name.Contains(term)));
        }

        var from = ParseDate(Request.Query["from"]);
        var to = ParseDate(Request.Query["to"]);

        if (from is { } f) invoices = invoices.Where(s => s.SaleDate >= f);
        if (to is { } t) invoices = invoices.Where(s => s.SaleDate <= t);

        if (Request.Query["dues"] == "true")
            invoices = invoices.Where(s => s.PaidAmount < s.GrandTotal);

        var sorted = invoices.ApplySort(query.Sort, query.Desc, Sortable, "date");
        if (string.IsNullOrWhiteSpace(query.Sort)) sorted = sorted.OrderByDescending(s => s.SaleDate);

        return new SaleListViewModel
        {
            Page = await PagedList<Sale>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            From = from,
            To = to,
            OnlyWithDues = Request.Query["dues"] == "true",
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(CancellationToken ct)
    {
        var model = new SaleFormViewModel
        {
            SaleDate = DateOnly.FromDateTime(DateTime.Today),
            // One blank line to start; more are added in the browser.
            Items = [new SaleLineViewModel()],
        };

        await FillOptionsAsync(model, ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaleFormViewModel model, CancellationToken ct)
    {
        var lines = model.Items.Where(i => i.IsFilled).ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one line with a species, quantity and price.");

        if (model.SaleDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.SaleDate), "A sale cannot be dated in the future.");

        // Selling more than the batch holds would drive its quantity negative.
        foreach (var line in lines.Where(l => l.FishStockId is > 0))
        {
            var batch = await db.FishStocks.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == line.FishStockId, ct);

            if (batch is null) continue;

            var availableKg = batch.CurrentQuantity * batch.CurrentAvgWeightGram / 1000m;
            if (line.QuantityKg > availableKg)
                ModelState.AddModelError(string.Empty,
                    $"That batch holds about {availableKg:N1} kg; you entered {line.QuantityKg:N1} kg.");
        }

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        var sale = new Sale
        {
            CustomerId = model.CustomerId == 0 ? null : model.CustomerId,
            SaleDate = model.SaleDate,
            Discount = model.Discount,
            PaymentMethod = model.PaymentMethod,
            PaidAmount = model.PaidAmount,
            Notes = model.Notes,
            Items = [.. lines.Select(l => new SaleItem
            {
                FishTypeId = l.FishTypeId,
                PondId = l.PondId == 0 ? null : l.PondId,
                FishStockId = l.FishStockId == 0 ? null : l.FishStockId,
                QuantityKg = l.QuantityKg!.Value,
                PieceCount = l.PieceCount,
                UnitPrice = l.UnitPrice!.Value,
            })],
        };

        await sales.CreateAsync(sale, deductFromStock: true, ct);

        return Saved($"Sale {sale.InvoiceNo} recorded for {sale.GrandTotal:N2}.");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var sale = await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.FishType)
            .Include(s => s.Items).ThenInclude(i => i.Pond)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (sale is null) return NotFound();

        ViewData["Title"] = $"Invoice {sale.InvoiceNo}";
        ViewData["Subtitle"] = sale.SaleDate.ToString("dd MMM yyyy");
        return View(sale);
    }

    // ----- Payment ------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> PaymentForm(int id, CancellationToken ct)
    {
        var sale = await db.Sales.Include(s => s.Customer)
            .AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

        if (sale is null) return NotFound();

        return PartialView("_PaymentForm", new SalePaymentViewModel
        {
            SaleId = sale.Id,
            InvoiceNo = sale.InvoiceNo,
            CustomerName = sale.Customer?.Name ?? "Walk-in",
            DueAmount = sale.DueAmount,
            Amount = sale.DueAmount,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceivePayment(SalePaymentViewModel model, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == model.SaleId, ct);
        if (sale is null) return NotFound();

        if (model.Amount > sale.DueAmount)
            ModelState.AddModelError(nameof(model.Amount),
                $"Only {sale.DueAmount:N2} is outstanding on this invoice.");

        if (!ModelState.IsValid)
        {
            model.DueAmount = sale.DueAmount;
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_PaymentForm", model);
        }

        await sales.ReceiveDueAsync(model.SaleId, model.Amount, model.PaymentMethod, ct);
        return Saved($"Received {model.Amount:N2} against {sale.InvoiceNo}.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete sale",
            Message = $"Delete invoice {sale.InvoiceNo}?",
            Detail = "The harvest it recorded is reversed, the cash entry removed and the customer balance corrected.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null) return NotFound();

        await sales.DeleteAsync(id, ct);
        return Saved($"Sale {sale.InvoiceNo} deleted.");
    }

    // ----- Cascading batch picker ---------------------------------------------------------

    /// <summary>
    /// Returns the active batches in a pond, so choosing a pond narrows the batch list on the
    /// line without reloading the whole form.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BatchOptions(int pondId, int index, int? selected, CancellationToken ct)
    {
        var batches = pondId <= 0
            ? []
            : await db.FishStocks.AsNoTracking()
                .Where(s => s.PondId == pondId && s.Status == StockStatus.Active && s.CurrentQuantity > 0)
                .Include(s => s.FishType)
                .OrderBy(s => s.StockDate)
                .Select(s => new SelectOption(
                    s.Id,
                    s.FishType!.Name + " - " + s.CurrentQuantity.ToString("N0") + " pcs at "
                        + s.CurrentAvgWeightGram.ToString("N0") + " g"))
                .ToListAsync(ct);

        ViewData["Index"] = index;
        ViewData["Selected"] = selected;
        return PartialView("_BatchOptions", batches);
    }

    private async Task FillOptionsAsync(SaleFormViewModel model, CancellationToken ct)
    {
        model.Customers = await db.Customers.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.Name).Select(c => new SelectOption(c.Id, c.Name)).ToListAsync(ct);

        model.Species = await db.FishTypes.AsNoTracking()
            .OrderBy(f => f.Name).Select(f => new SelectOption(f.Id, f.Name)).ToListAsync(ct);

        model.Ponds = await db.Ponds.AsNoTracking().Where(p => p.Status == PondStatus.Active)
            .OrderBy(p => p.Name).Select(p => new SelectOption(p.Id, p.Name)).ToListAsync(ct);
    }

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParse(value, out var date) ? date : null;
}
