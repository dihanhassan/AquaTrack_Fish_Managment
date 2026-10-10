using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Water chemistry readings. Out-of-range values are flagged on the row, because bad water is the
/// usual explanation when mortality climbs and nothing else has changed.
/// </summary>
[Authorize(Policy = Policies.RecordDailyData)]
public class WaterQualityController(IApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(ListQuery query, int? pondId, CancellationToken ct)
    {
        ViewData["Title"] = "Water Quality";
        ViewData["Subtitle"] = "pH, temperature, oxygen and ammonia by pond";

        return View(await BuildListAsync(query, pondId, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, int? pondId, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, pondId, ct));

    private async Task<WaterQualityListViewModel> BuildListAsync(
        ListQuery query, int? pondId, CancellationToken ct)
    {
        var logs = db.WaterQualityLogs.Include(w => w.Pond).AsNoTracking();

        if (pondId is { } pond)
            logs = logs.Where(w => w.PondId == pond);

        var sorted = logs.OrderByDescending(w => w.RecordDate).ThenByDescending(w => w.Id);

        return new WaterQualityListViewModel
        {
            Page = await PagedList<WaterQualityLog>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            PondId = pondId,
            Ponds = await PondOptionsAsync(ct),
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, int? pondId, CancellationToken ct)
    {
        var model = new WaterQualityFormViewModel
        {
            PondId = pondId ?? 0,
            RecordDate = DateOnly.FromDateTime(DateTime.Today),
        };

        if (id is { } logId)
        {
            var log = await db.WaterQualityLogs.AsNoTracking().FirstOrDefaultAsync(w => w.Id == logId, ct);
            if (log is null) return NotFound();

            model = new WaterQualityFormViewModel
            {
                Id = log.Id,
                PondId = log.PondId,
                RecordDate = log.RecordDate,
                Ph = log.Ph,
                TemperatureC = log.TemperatureC,
                DissolvedOxygenMgL = log.DissolvedOxygenMgL,
                AmmoniaMgL = log.AmmoniaMgL,
                NitriteMgL = log.NitriteMgL,
                TurbidityCm = log.TurbidityCm,
                Notes = log.Notes,
            };
        }

        model.Ponds = await PondOptionsAsync(ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WaterQualityFormViewModel model, CancellationToken ct)
    {
        if (!model.HasAnyReading)
            ModelState.AddModelError(string.Empty, "Enter at least one reading.");

        if (model.RecordDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.RecordDate), "A reading cannot be dated in the future.");

        if (!ModelState.IsValid)
        {
            model.Ponds = await PondOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        WaterQualityLog log;
        if (model.Id is { } id)
        {
            log = await db.WaterQualityLogs.FirstOrDefaultAsync(w => w.Id == id, ct)
                  ?? throw new InvalidOperationException($"Reading {id} not found.");
        }
        else
        {
            log = new WaterQualityLog();
            db.WaterQualityLogs.Add(log);
        }

        log.PondId = model.PondId;
        log.RecordDate = model.RecordDate;
        log.Ph = model.Ph;
        log.TemperatureC = model.TemperatureC;
        log.DissolvedOxygenMgL = model.DissolvedOxygenMgL;
        log.AmmoniaMgL = model.AmmoniaMgL;
        log.NitriteMgL = model.NitriteMgL;
        log.TurbidityCm = model.TurbidityCm;
        log.Notes = model.Notes;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? "Reading recorded." : "Reading updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var log = await db.WaterQualityLogs.Include(w => w.Pond)
            .AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);

        if (log is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete reading",
            Message = $"Delete the {log.RecordDate:dd MMM yyyy} reading for {log.Pond?.Name}?",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var log = await db.WaterQualityLogs.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (log is null) return NotFound();

        db.WaterQualityLogs.Remove(log);
        await db.SaveChangesAsync(ct);

        return Saved("Reading deleted.");
    }

    private async Task<IReadOnlyList<SelectOption>> PondOptionsAsync(CancellationToken ct)
        => await db.Ponds.AsNoTracking()
            .Where(p => p.Status != PondStatus.Inactive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectOption(p.Id, p.Name))
            .ToListAsync(ct);
}
