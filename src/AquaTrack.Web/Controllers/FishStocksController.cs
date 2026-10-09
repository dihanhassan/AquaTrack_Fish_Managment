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
/// Fish batches, plus the events that change them: growth samples, mortality, harvest and manual
/// adjustments. Each event re-derives the batch totals through <see cref="IStockService"/> rather
/// than editing them directly, so the figures can always be rebuilt from history.
/// </summary>
public class FishStocksController(IApplicationDbContext db, IStockService stock) : AppController
{
    private static readonly Dictionary<string, Expression<Func<FishStock, object>>> Sortable = new()
    {
        ["pond"] = s => s.Pond!.Name,
        ["species"] = s => s.FishType!.Name,
        ["date"] = s => s.StockDate,
        ["quantity"] = s => s.CurrentQuantity,
        ["weight"] = s => s.CurrentAvgWeightGram,
    };

    public async Task<IActionResult> Index(ListQuery query, int? pondId, CancellationToken ct)
    {
        ViewData["Title"] = "Fish Stock";
        ViewData["Subtitle"] = "Batches currently in the water";

        return View(await BuildListAsync(query, pondId, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, int? pondId, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, pondId, ct));

    private async Task<FishStockListViewModel> BuildListAsync(ListQuery query, int? pondId, CancellationToken ct)
    {
        var batches = db.FishStocks.Include(s => s.Pond).Include(s => s.FishType).AsNoTracking();

        if (pondId is { } pond)
            batches = batches.Where(s => s.PondId == pond);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            batches = batches.Where(s =>
                s.Pond!.Name.Contains(term) ||
                s.FishType!.Name.Contains(term) ||
                (s.BatchCode != null && s.BatchCode.Contains(term)));
        }

        if (Enum.TryParse<StockStatus>(Request.Query["status"], out var status))
            batches = batches.Where(s => s.Status == status);

        var sorted = batches.ApplySort(query.Sort, query.Desc, Sortable, "date");

        return new FishStockListViewModel
        {
            Page = await PagedList<FishStock>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            PondId = pondId,
            StatusFilter = Request.Query["status"],
            Ponds = await PondOptionsAsync(ct),
        };
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, int? pondId, CancellationToken ct)
    {
        var model = new FishStockFormViewModel
        {
            PondId = pondId ?? 0,
            StockDate = DateOnly.FromDateTime(DateTime.Today),
        };

        if (id is { } stockId)
        {
            var batch = await db.FishStocks.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stockId, ct);
            if (batch is null) return NotFound();

            model = new FishStockFormViewModel
            {
                Id = batch.Id,
                PondId = batch.PondId,
                FishTypeId = batch.FishTypeId,
                BatchCode = batch.BatchCode,
                StockDate = batch.StockDate,
                InitialQuantity = batch.InitialQuantity,
                InitialAvgWeightGram = batch.InitialAvgWeightGram,
                UnitCost = batch.UnitCost,
                Source = batch.Source,
                Status = batch.Status,
                Notes = batch.Notes,
            };
        }

        await FillOptionsAsync(model, ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FishStockFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        FishStock batch;
        if (model.Id is { } id)
        {
            batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == id, ct)
                    ?? throw new InvalidOperationException($"Batch {id} not found.");
        }
        else
        {
            batch = new FishStock
            {
                // A new batch starts whole; mortality and harvest reduce it from here.
                CurrentQuantity = model.InitialQuantity,
                CurrentAvgWeightGram = model.InitialAvgWeightGram,
            };
            db.FishStocks.Add(batch);
        }

        batch.PondId = model.PondId;
        batch.FishTypeId = model.FishTypeId;
        batch.BatchCode = model.BatchCode;
        batch.StockDate = model.StockDate;
        batch.InitialQuantity = model.InitialQuantity;
        batch.InitialAvgWeightGram = model.InitialAvgWeightGram;
        batch.UnitCost = model.UnitCost;
        batch.Source = model.Source;
        batch.Status = model.Status;
        batch.Notes = model.Notes;

        await db.SaveChangesAsync(ct);

        // Editing the initial quantity shifts the live figure, so recompute from the events.
        if (model.Id is not null)
            await stock.RecalculateFishStockAsync(batch.Id, ct);

        return Saved(model.Id is null ? "Batch stocked." : "Batch updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var batch = await db.FishStocks
            .Include(s => s.Pond).Include(s => s.FishType)
            .AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

        if (batch is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete batch",
            Message = $"Delete the {batch.FishType?.Name} batch in {batch.Pond?.Name}?",
            Detail = "Its growth, mortality and harvest records are kept for reporting.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (batch is null) return NotFound();

        var isSold = await db.SaleItems.AnyAsync(i => i.FishStockId == id, ct);
        if (isSold)
            return Failed("This batch has been sold from and cannot be deleted. Mark it harvested instead.");

        db.FishStocks.Remove(batch);
        await db.SaveChangesAsync(ct);

        return Saved("Batch deleted.");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var batch = await db.FishStocks
            .Include(s => s.Pond).Include(s => s.FishType)
            .AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

        if (batch is null) return NotFound();

        var model = new FishStockDetailsViewModel
        {
            Batch = batch,
            GrowthRecords = await db.FishGrowths.Where(g => g.FishStockId == id)
                .OrderByDescending(g => g.RecordDate).Take(30).AsNoTracking().ToListAsync(ct),
            MortalityRecords = await db.FishMortalities.Where(m => m.FishStockId == id)
                .OrderByDescending(m => m.RecordDate).Take(30).AsNoTracking().ToListAsync(ct),
            Harvests = await db.Harvests.Where(h => h.FishStockId == id)
                .OrderByDescending(h => h.HarvestDate).Take(30).AsNoTracking().ToListAsync(ct),
            Adjustments = await db.StockAdjustments.Where(a => a.FishStockId == id)
                .OrderByDescending(a => a.AdjustmentDate).Take(30).AsNoTracking().ToListAsync(ct),
        };

        ViewData["Title"] = $"{batch.FishType?.Name} in {batch.Pond?.Name}";
        ViewData["Subtitle"] = $"Stocked {batch.StockDate:dd MMM yyyy}";
        return View(model);
    }

    // ----- Batch events -------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.RecordDailyData)]
    public IActionResult GrowthForm(int fishStockId)
        => PartialView("_GrowthForm", new GrowthFormViewModel
        {
            FishStockId = fishStockId,
            RecordDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.RecordDailyData)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGrowth(GrowthFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_GrowthForm", model);
        }

        db.FishGrowths.Add(new FishGrowth
        {
            FishStockId = model.FishStockId,
            RecordDate = model.RecordDate,
            SampleSize = model.SampleSize,
            AvgWeightGram = model.AvgWeightGram,
            AvgLengthCm = model.AvgLengthCm,
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(model.FishStockId, ct);

        return Saved("Growth sample recorded.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.RecordDailyData)]
    public IActionResult MortalityForm(int fishStockId)
        => PartialView("_MortalityForm", new MortalityFormViewModel
        {
            FishStockId = fishStockId,
            RecordDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.RecordDailyData)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMortality(MortalityFormViewModel model, CancellationToken ct)
    {
        var batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == model.FishStockId, ct);
        if (batch is null) return NotFound();

        if (model.Quantity > batch.CurrentQuantity)
            ModelState.AddModelError(nameof(model.Quantity),
                $"Only {batch.CurrentQuantity:N0} fish remain in this batch.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_MortalityForm", model);
        }

        db.FishMortalities.Add(new FishMortality
        {
            FishStockId = model.FishStockId,
            RecordDate = model.RecordDate,
            Quantity = model.Quantity,
            Cause = model.Cause,
            AvgWeightGram = batch.CurrentAvgWeightGram,
            // Valued at what the fish cost to stock, which is the recoverable loss.
            EstimatedLoss = Math.Round(model.Quantity * batch.UnitCost, 2),
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(model.FishStockId, ct);

        return Saved("Mortality recorded.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public IActionResult HarvestForm(int fishStockId)
        => PartialView("_HarvestForm", new HarvestFormViewModel
        {
            FishStockId = fishStockId,
            HarvestDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHarvest(HarvestFormViewModel model, CancellationToken ct)
    {
        var batch = await db.FishStocks.FirstOrDefaultAsync(s => s.Id == model.FishStockId, ct);
        if (batch is null) return NotFound();

        if (model.Quantity > batch.CurrentQuantity)
            ModelState.AddModelError(nameof(model.Quantity),
                $"Only {batch.CurrentQuantity:N0} fish remain in this batch.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_HarvestForm", model);
        }

        db.Harvests.Add(new Harvest
        {
            FishStockId = model.FishStockId,
            HarvestDate = model.HarvestDate,
            Quantity = model.Quantity,
            TotalWeightKg = model.TotalWeightKg,
            ExpectedUnitPrice = model.ExpectedUnitPrice,
            IsPartial = model.Quantity < batch.CurrentQuantity,
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(model.FishStockId, ct);

        return Saved("Harvest recorded.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public IActionResult AdjustmentForm(int fishStockId)
        => PartialView("_AdjustmentForm", new AdjustmentFormViewModel
        {
            FishStockId = fishStockId,
            AdjustmentDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAdjustment(AdjustmentFormViewModel model, CancellationToken ct)
    {
        if (model.QuantityDelta == 0)
            ModelState.AddModelError(nameof(model.QuantityDelta), "Enter how many fish to add or remove.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_AdjustmentForm", model);
        }

        db.StockAdjustments.Add(new StockAdjustment
        {
            FishStockId = model.FishStockId,
            AdjustmentDate = model.AdjustmentDate,
            QuantityDelta = model.QuantityDelta,
            Reason = model.Reason,
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await stock.RecalculateFishStockAsync(model.FishStockId, ct);

        return Saved("Stock adjusted.");
    }

    // ----- Helpers ------------------------------------------------------------------------

    private async Task FillOptionsAsync(FishStockFormViewModel model, CancellationToken ct)
    {
        model.Ponds = await PondOptionsAsync(ct);
        model.Species = await db.FishTypes.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new SelectOption(f.Id, f.Name))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<SelectOption>> PondOptionsAsync(CancellationToken ct)
        => await db.Ponds.AsNoTracking()
            .Where(p => p.Status != PondStatus.Inactive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectOption(p.Id, p.Name))
            .ToListAsync(ct);
}
