using System.Linq.Expressions;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Reference implementation of the list/modal-form CRUD pattern every module follows:
/// Index renders the page, List returns the table partial, Form returns the modal form,
/// and Save/Delete answer with HX-Trigger events rather than HTML.
/// </summary>
public class PondsController(IApplicationDbContext db) : AppController
{
    /// <summary>Whitelist of sortable columns; anything else falls back to the default.</summary>
    private static readonly Dictionary<string, Expression<Func<Pond, object>>> Sortable = new()
    {
        ["name"] = p => p.Name,
        ["area"] = p => p.AreaDecimal,
        ["depth"] = p => p.DepthMetre,
        ["status"] = p => p.Status,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Ponds";
        ViewData["Subtitle"] = "Every culture unit on the farm";

        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<PondListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var ponds = db.Ponds.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            ponds = ponds.Where(p =>
                p.Name.Contains(term) ||
                (p.Code != null && p.Code.Contains(term)) ||
                (p.Location != null && p.Location.Contains(term)));
        }

        if (Enum.TryParse<PondStatus>(Request.Query["status"], out var status))
            ponds = ponds.Where(p => p.Status == status);

        var projected = ponds
            .ApplySort(query.Sort, query.Desc, Sortable, "name")
            .Select(p => new PondRow
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                AreaDecimal = p.AreaDecimal,
                DepthMetre = p.DepthMetre,
                WaterType = p.WaterType,
                Status = p.Status,
                Location = p.Location,
                // Only active batches count toward the live figures shown in the list.
                ActiveBatches = p.FishStocks.Count(s => s.Status == StockStatus.Active),
                FishCount = p.FishStocks.Where(s => s.Status == StockStatus.Active).Sum(s => s.CurrentQuantity),
                BiomassGram = p.FishStocks.Where(s => s.Status == StockStatus.Active)
                    .Sum(s => s.CurrentQuantity * s.CurrentAvgWeightGram),
            });

        return new PondListViewModel
        {
            Page = await PagedList<PondRow>.CreateAsync(projected, query.Page, query.PageSize, ct),
            Query = query,
            StatusFilter = Request.Query["status"],
        };
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        if (id is null) return PartialView("_Form", new PondFormViewModel());

        var pond = await db.Ponds.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pond is null) return NotFound();

        return PartialView("_Form", new PondFormViewModel
        {
            Id = pond.Id,
            Name = pond.Name,
            Code = pond.Code,
            AreaDecimal = pond.AreaDecimal,
            DepthMetre = pond.DepthMetre,
            WaterType = pond.WaterType,
            Status = pond.Status,
            Location = pond.Location,
            CommissionedOn = pond.CommissionedOn,
            Notes = pond.Notes,
        });
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PondFormViewModel model, CancellationToken ct)
    {
        // A pond code is optional, but must be unique when given.
        if (!string.IsNullOrWhiteSpace(model.Code))
        {
            var codeTaken = await db.Ponds
                .AnyAsync(p => p.Code == model.Code && p.Id != model.Id, ct);

            if (codeTaken)
                ModelState.AddModelError(nameof(model.Code), "Another pond already uses this code.");
        }

        if (!ModelState.IsValid) return UnprocessableForm(model);

        Pond pond;
        if (model.Id is { } id)
        {
            pond = await db.Ponds.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new InvalidOperationException();
        }
        else
        {
            pond = new Pond();
            db.Ponds.Add(pond);
        }

        pond.Name = model.Name.Trim();
        pond.Code = string.IsNullOrWhiteSpace(model.Code) ? null : model.Code.Trim();
        pond.AreaDecimal = model.AreaDecimal;
        pond.DepthMetre = model.DepthMetre;
        pond.WaterType = model.WaterType;
        pond.Status = model.Status;
        pond.Location = model.Location;
        pond.CommissionedOn = model.CommissionedOn;
        pond.Notes = model.Notes;

        await db.SaveChangesAsync(ct);

        return Saved(model.Id is null ? $"Pond \"{pond.Name}\" added." : $"Pond \"{pond.Name}\" updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var pond = await db.Ponds.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pond is null) return NotFound();

        var batches = await db.FishStocks.CountAsync(s => s.PondId == id && s.Status == StockStatus.Active, ct);

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete pond",
            Message = $"Delete \"{pond.Name}\"?",
            Detail = batches > 0
                ? $"This pond still holds {batches} active batch(es). Harvest or move them first."
                : "Past records stay in reports; the pond is only hidden from new entries.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var pond = await db.Ponds.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pond is null) return NotFound();

        // Refuse while fish are still in the water, or the stock figures stop adding up.
        var hasActiveStock = await db.FishStocks.AnyAsync(s => s.PondId == id && s.Status == StockStatus.Active, ct);
        if (hasActiveStock)
            return Failed("This pond still holds active fish stock. Harvest or move it first.");

        db.Ponds.Remove(pond);
        await db.SaveChangesAsync(ct);

        return Saved($"Pond \"{pond.Name}\" deleted.");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var pond = await db.Ponds.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pond is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var model = new PondDetailsViewModel
        {
            Pond = pond,
            Batches = await db.FishStocks
                .Include(s => s.FishType)
                .Where(s => s.PondId == id)
                .OrderByDescending(s => s.StockDate)
                .AsNoTracking()
                .ToListAsync(ct),
            FeedThisMonth = await db.FeedUsages
                .Where(u => u.PondId == id && u.UsageDate >= monthStart)
                .SumAsync(u => (decimal?)u.Quantity, ct) ?? 0m,
            FeedCostThisMonth = await db.FeedUsages
                .Where(u => u.PondId == id && u.UsageDate >= monthStart)
                .SumAsync(u => (decimal?)u.TotalCost, ct) ?? 0m,
            MedicineCostThisMonth = await db.MedicineUsages
                .Where(u => u.PondId == id && u.UsageDate >= monthStart)
                .SumAsync(u => (decimal?)u.TotalCost, ct) ?? 0m,
            RecentWaterQuality = await db.WaterQualityLogs
                .Where(w => w.PondId == id)
                .OrderByDescending(w => w.RecordDate)
                .Take(5)
                .AsNoTracking()
                .ToListAsync(ct),
        };

        ViewData["Title"] = pond.Name;
        ViewData["Subtitle"] = $"{pond.AreaDecimal:N1} decimal, {pond.DepthMetre:N1} m deep";
        return View(model);
    }

    /// <summary>Re-renders the modal form with validation errors and a 422 the client swaps in.</summary>
    private IActionResult UnprocessableForm(PondFormViewModel model)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_Form", model);
    }
}
