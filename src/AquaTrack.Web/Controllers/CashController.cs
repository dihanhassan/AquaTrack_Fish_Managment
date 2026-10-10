using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Services;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The cash book. Every module posts here, so this is the one place the whole farm's money
/// movement can be read in order, with a running balance.
/// </summary>
[Authorize(Policy = Policies.ManageFarm)]
public class CashController(IApplicationDbContext db, ICashBookService cashBook) : AppController
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var model = await BuildAsync(from, to, ct);

        ViewData["Title"] = "Cash Book";
        ViewData["Subtitle"] = "Opening + In − Out = Closing";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Ledger(DateOnly? from, DateOnly? to, CancellationToken ct)
        => PartialView("_Ledger", await BuildAsync(from, to, ct));

    private async Task<CashBookViewModel> BuildAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;

        // A reversed range would silently return nothing, so swap rather than show an empty page.
        if (start > end) (start, end) = (end, start);

        var ledger = await cashBook.GetLedgerAsync(start, end, ct);
        var summary = await cashBook.GetSummaryAsync(start, end, ct);

        var byCategory = ledger
            .GroupBy(row => new { row.Transaction.Category, row.Transaction.Direction })
            .Select(g => new CashCategoryTotal(
                g.Key.Category,
                g.Key.Direction,
                g.Sum(row => row.Transaction.Amount)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new CashBookViewModel
        {
            From = start,
            To = end,
            Summary = summary,
            Rows = ledger,
            ByCategory = byCategory,
        };
    }

    [HttpGet]
    public IActionResult Form()
        => PartialView("_Form", new CashEntryFormViewModel
        {
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CashEntryFormViewModel model, CancellationToken ct)
    {
        if (model.TransactionDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.TransactionDate), "A cash entry cannot be dated in the future.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        await cashBook.PostAsync(
            model.TransactionDate,
            model.Direction,
            model.Amount,
            model.Category.Trim(),
            model.PaymentMethod,
            model.Description,
            ReferenceType.Manual,
            referenceId: null,
            systemGenerated: false,
            ct: ct);

        var wording = model.Direction == CashDirection.In ? "received" : "paid out";
        return Saved($"{model.Amount:N2} {wording} recorded.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var entry = await db.CashTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();

        if (entry.IsSystemGenerated)
            return Failed("This entry belongs to another document. Delete that document instead.");

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete cash entry",
            Message = $"Delete the {entry.Amount:N2} {entry.Category} entry?",
            Detail = "The running balance from this date onwards will change.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entry = await db.CashTransactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();

        // Entries created by a document must be reversed by deleting that document, or the
        // document and the ledger stop agreeing.
        if (entry.IsSystemGenerated)
            return Failed("This entry belongs to another document. Delete that document instead.");

        db.CashTransactions.Remove(entry);
        await db.SaveChangesAsync(ct);

        return Saved("Cash entry deleted.");
    }
}
