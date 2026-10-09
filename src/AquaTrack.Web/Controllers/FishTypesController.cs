using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Entities;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The species catalogue. Each entry carries the growth constants the AI module uses, so adding a
/// species is also what teaches the system how that fish is expected to grow and eat.
/// </summary>
public class FishTypesController(IApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Species";
        ViewData["Subtitle"] = "Fish species and their growth profiles";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<FishTypeListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var species = db.FishTypes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            species = species.Where(f =>
                f.Name.Contains(term) ||
                (f.LocalName != null && f.LocalName.Contains(term)) ||
                (f.ScientificName != null && f.ScientificName.Contains(term)));
        }

        var projected = species
            .OrderBy(f => f.Name)
            .Select(f => new FishTypeRow
            {
                Id = f.Id,
                Name = f.Name,
                LocalName = f.LocalName,
                ScientificName = f.ScientificName,
                TargetWeightGram = f.TargetWeightGram,
                GrowOutDays = f.GrowOutDays,
                DefaultFeedRatePercent = f.DefaultFeedRatePercent,
                ActiveBatches = f.FishStocks.Count(s => s.Status == Domain.Enums.StockStatus.Active),
            });

        return new FishTypeListViewModel
        {
            Page = await PagedList<FishTypeRow>.CreateAsync(projected, query.Page, query.PageSize, ct),
            Query = query,
        };
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        if (id is null) return PartialView("_Form", new FishTypeFormViewModel());

        var species = await db.FishTypes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (species is null) return NotFound();

        return PartialView("_Form", new FishTypeFormViewModel
        {
            Id = species.Id,
            Name = species.Name,
            LocalName = species.LocalName,
            ScientificName = species.ScientificName,
            TargetWeightGram = species.TargetWeightGram,
            GrowOutDays = species.GrowOutDays,
            DefaultFeedRatePercent = species.DefaultFeedRatePercent,
            Description = species.Description,
        });
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FishTypeFormViewModel model, CancellationToken ct)
    {
        var nameTaken = await db.FishTypes.AnyAsync(f => f.Name == model.Name && f.Id != model.Id, ct);
        if (nameTaken)
            ModelState.AddModelError(nameof(model.Name), "A species with this name already exists.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        FishType species;
        if (model.Id is { } id)
        {
            species = await db.FishTypes.FirstOrDefaultAsync(f => f.Id == id, ct)
                      ?? throw new InvalidOperationException($"Species {id} not found.");
        }
        else
        {
            species = new FishType();
            db.FishTypes.Add(species);
        }

        species.Name = model.Name.Trim();
        species.LocalName = model.LocalName;
        species.ScientificName = model.ScientificName;
        species.TargetWeightGram = model.TargetWeightGram;
        species.GrowOutDays = model.GrowOutDays;
        species.DefaultFeedRatePercent = model.DefaultFeedRatePercent;
        species.Description = model.Description;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"Species \"{species.Name}\" added." : "Species updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var species = await db.FishTypes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (species is null) return NotFound();

        var batches = await db.FishStocks.CountAsync(s => s.FishTypeId == id, ct);

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete species",
            Message = $"Delete \"{species.Name}\"?",
            Detail = batches > 0
                ? $"{batches} batch(es) reference this species and will keep their history."
                : "This species has never been stocked.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var species = await db.FishTypes.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (species is null) return NotFound();

        var hasActiveStock = await db.FishStocks
            .AnyAsync(s => s.FishTypeId == id && s.Status == Domain.Enums.StockStatus.Active, ct);

        if (hasActiveStock)
            return Failed("This species is in an active batch and cannot be deleted.");

        db.FishTypes.Remove(species);
        await db.SaveChangesAsync(ct);

        return Saved($"Species \"{species.Name}\" deleted.");
    }
}
