using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Services;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The screen a worker uses every day: pick a pond and a date, fill in whatever happened, save
/// once. The whole form is handed to <see cref="IDailyLogService"/>, which writes every record it
/// implies inside one transaction.
/// </summary>
[Authorize(Policy = Policies.RecordDailyData)]
public class DailyInputController(
    IApplicationDbContext db,
    IDailyLogService dailyLog,
    IFeedAdvisorAccessor advisor) : AppController
{
    public async Task<IActionResult> Index(int? pondId, DateOnly? date, CancellationToken ct)
    {
        var model = await BuildAsync(pondId, date ?? DateOnly.FromDateTime(DateTime.Today), ct);

        ViewData["Title"] = "Daily Input";
        ViewData["Subtitle"] = model.LogDate.ToString("dddd, dd MMMM yyyy");
        return View(model);
    }

    /// <summary>Re-renders the form when the pond or date changes, so the context panel stays correct.</summary>
    [HttpGet]
    public async Task<IActionResult> Form(int? pondId, DateOnly? date, CancellationToken ct)
        => PartialView("_Form", await BuildAsync(pondId, date ?? DateOnly.FromDateTime(DateTime.Today), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DailyInputViewModel model, CancellationToken ct)
    {
        if (model.PondId <= 0)
            ModelState.AddModelError(nameof(model.PondId), "Choose a pond.");

        if (model.LogDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.LogDate), "You cannot record a future date.");

        if (!model.HasAnything)
            ModelState.AddModelError(string.Empty, "Nothing was entered. Fill in at least one section.");

        if (!ModelState.IsValid)
            return await RedisplayAsync(model, ct);

        try
        {
            await dailyLog.SaveAsync(model.ToInput(), ct);
        }
        catch (InsufficientStockException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RedisplayAsync(model, ct);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RedisplayAsync(model, ct);
        }

        // Reload the same pond and date so the user can see what was saved and keep going.
        var refreshed = await BuildAsync(model.PondId, model.LogDate, ct);
        Response.HxToast($"Saved the day for {refreshed.PondName}.");

        return PartialView("_Form", refreshed);
    }

    private async Task<IActionResult> RedisplayAsync(DailyInputViewModel model, CancellationToken ct)
    {
        await FillContextAsync(model, ct);
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_Form", model);
    }

    private async Task<DailyInputViewModel> BuildAsync(int? pondId, DateOnly date, CancellationToken ct)
    {
        var ponds = await db.Ponds.AsNoTracking()
            .Where(p => p.Status == PondStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new SelectOption(p.Id, p.Name))
            .ToListAsync(ct);

        var selected = pondId ?? ponds.FirstOrDefault()?.Id ?? 0;

        var model = new DailyInputViewModel
        {
            PondId = selected,
            LogDate = date,
            // Two blank feed rows is the common case; more can be added in the browser.
            FeedLines = [new DailyFeedLineViewModel(), new DailyFeedLineViewModel()],
            MedicineLines = [new DailyMedicineLineViewModel()],
        };

        await FillContextAsync(model, ct);
        return model;
    }

    /// <summary>Loads the dropdowns plus the panel that shows what is already recorded for the day.</summary>
    private async Task FillContextAsync(DailyInputViewModel model, CancellationToken ct)
    {
        model.Ponds = await db.Ponds.AsNoTracking()
            .Where(p => p.Status == PondStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new SelectOption(p.Id, p.Name))
            .ToListAsync(ct);

        model.FeedItems = await db.FeedItems.AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .Select(f => new SelectOption(f.Id, f.Name + " (" + f.CurrentStock.ToString("N1") + " left)"))
            .ToListAsync(ct);

        model.Medicines = await db.Medicines.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new SelectOption(m.Id, m.Name))
            .ToListAsync(ct);

        model.Species = await db.FishTypes.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new SelectOption(f.Id, f.Name))
            .ToListAsync(ct);

        model.Customers = await db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectOption(c.Id, c.Name))
            .ToListAsync(ct);

        model.ExpenseCategories = await db.ExpenseCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectOption(c.Id, c.Name))
            .ToListAsync(ct);

        if (model.PondId <= 0) return;

        model.PondName = model.Ponds.FirstOrDefault(p => p.Id == model.PondId)?.Name ?? "-";

        model.Batches = await db.FishStocks.AsNoTracking()
            .Where(s => s.PondId == model.PondId && s.Status == StockStatus.Active)
            .Include(s => s.FishType)
            .OrderBy(s => s.StockDate)
            .Select(s => new SelectOption(
                s.Id,
                (s.FishType!.Name) + " - " + s.CurrentQuantity.ToString("N0") + " pcs at "
                    + s.CurrentAvgWeightGram.ToString("N0") + " g"))
            .ToListAsync(ct);

        // A single active batch is selected for the user; several and they must choose.
        if (model.FishStockId is null && model.Batches.Count == 1)
            model.FishStockId = model.Batches[0].Id;

        var existing = await dailyLog.GetAsync(model.PondId, model.LogDate, ct);
        if (existing is not null)
        {
            model.AlreadyRecorded = new DailyLogSummary(
                existing.TotalFeedKg, existing.TotalFeedCost, existing.TotalMedicineCost,
                existing.TotalMortality, existing.TotalSales, existing.TotalOtherIncome,
                existing.TotalExpense, existing.IsLocked);

            model.Weather ??= existing.Weather;
            model.Observations ??= existing.Observations;
        }

        model.FeedAdvice = await advisor.GetAdviceAsync(model.PondId, ct);
    }
}

/// <summary>
/// Narrow seam over the feed advisor so the daily screen can show today's recommendation without
/// the controller depending on the whole AI surface.
/// </summary>
public interface IFeedAdvisorAccessor
{
    Task<string?> GetAdviceAsync(int pondId, CancellationToken ct = default);
}

public class FeedAdvisorAccessor(Application.Ai.IFeedAdvisor advisor) : IFeedAdvisorAccessor
{
    public async Task<string?> GetAdviceAsync(int pondId, CancellationToken ct = default)
    {
        var advice = await advisor.RecommendAsync(pondId, ct);
        if (advice.Count == 0) return null;

        var total = advice.Sum(a => a.RecommendedKgPerDay);
        return $"{total:N2} kg recommended today across {advice.Count} batch(es).";
    }
}
