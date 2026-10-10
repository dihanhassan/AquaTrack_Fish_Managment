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
/// Machines, their spare parts and their service history. Recording maintenance posts the repair
/// cost through the cash book, so upkeep shows up in the farm's accounts rather than being invisible.
/// </summary>
public class EquipmentController(IApplicationDbContext db, ICashBookService cashBook) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Equipment, object>>> Sortable = new()
    {
        ["name"] = e => e.Name,
        ["category"] = e => e.Category,
        ["purchased"] = e => e.PurchaseDate,
        ["cost"] = e => e.PurchasePrice,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Equipment";
        ViewData["Subtitle"] = "Machines, parts and maintenance";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<EquipmentListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var items = db.Equipments.Include(e => e.Supplier).Include(e => e.Pond).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(e =>
                e.Name.Contains(term) ||
                (e.Code != null && e.Code.Contains(term)) ||
                (e.Brand != null && e.Brand.Contains(term)));
        }

        if (Enum.TryParse<EquipmentCondition>(Request.Query["condition"], out var condition))
            items = items.Where(e => e.Condition == condition);

        var sorted = items.ApplySort(query.Sort, query.Desc, Sortable, "name");

        return new EquipmentListViewModel
        {
            Page = await PagedList<Equipment>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            ConditionFilter = Request.Query["condition"],
        };
    }

    // ----- Equipment ----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        var model = new EquipmentFormViewModel { PurchaseDate = DateOnly.FromDateTime(DateTime.Today) };

        if (id is { } equipmentId)
        {
            var item = await db.Equipments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == equipmentId, ct);
            if (item is null) return NotFound();

            model = new EquipmentFormViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Category = item.Category,
                Brand = item.Brand,
                ModelNo = item.ModelNo,
                SerialNo = item.SerialNo,
                SupplierId = item.SupplierId,
                PondId = item.PondId,
                PurchaseDate = item.PurchaseDate,
                PurchasePrice = item.PurchasePrice,
                Quantity = item.Quantity,
                Condition = item.Condition,
                WarrantyUntil = item.WarrantyUntil,
                Notes = item.Notes,
            };
        }

        await FillOptionsAsync(model, ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EquipmentFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        Equipment item;
        if (model.Id is { } id)
        {
            item = await db.Equipments.FirstOrDefaultAsync(e => e.Id == id, ct)
                   ?? throw new InvalidOperationException($"Equipment {id} not found.");
        }
        else
        {
            item = new Equipment();
            db.Equipments.Add(item);
        }

        item.Name = model.Name.Trim();
        item.Code = model.Code;
        item.Category = model.Category;
        item.Brand = model.Brand;
        item.ModelNo = model.ModelNo;
        item.SerialNo = model.SerialNo;
        item.SupplierId = model.SupplierId == 0 ? null : model.SupplierId;
        item.PondId = model.PondId == 0 ? null : model.PondId;
        item.PurchaseDate = model.PurchaseDate;
        item.PurchasePrice = model.PurchasePrice;
        item.Quantity = model.Quantity;
        item.Condition = model.Condition;
        item.WarrantyUntil = model.WarrantyUntil;
        item.Notes = model.Notes;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"\"{item.Name}\" added." : "Equipment updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var item = await db.Equipments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (item is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete equipment",
            Message = $"Delete \"{item.Name}\"?",
            Detail = "Its parts and maintenance history go with it.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await db.Equipments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (item is null) return NotFound();

        db.Equipments.Remove(item);
        await db.SaveChangesAsync(ct);

        return Saved($"\"{item.Name}\" deleted.");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var item = await db.Equipments
            .Include(e => e.Supplier).Include(e => e.Pond)
            .AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

        if (item is null) return NotFound();

        var model = new EquipmentDetailsViewModel
        {
            Equipment = item,
            Parts = await db.EquipmentParts.Where(p => p.EquipmentId == id)
                .OrderBy(p => p.Name).AsNoTracking().ToListAsync(ct),
            Maintenance = await db.EquipmentMaintenances.Where(m => m.EquipmentId == id)
                .OrderByDescending(m => m.MaintenanceDate).AsNoTracking().ToListAsync(ct),
        };

        ViewData["Title"] = item.Name;
        ViewData["Subtitle"] = $"{item.Category} · {item.Condition}";
        return View(model);
    }

    // ----- Parts --------------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public IActionResult PartForm(int equipmentId, int? id)
        => PartialView("_PartForm", new EquipmentPartFormViewModel
        {
            Id = id,
            EquipmentId = equipmentId,
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePart(EquipmentPartFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_PartForm", model);
        }

        db.EquipmentParts.Add(new EquipmentPart
        {
            EquipmentId = model.EquipmentId,
            Name = model.Name.Trim(),
            PartNo = model.PartNo,
            Unit = model.Unit,
            Quantity = model.Quantity,
            ReorderLevel = model.ReorderLevel,
            UnitPrice = model.UnitPrice,
            PurchaseDate = model.PurchaseDate,
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        return Saved($"Part \"{model.Name}\" added.");
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> DeletePart(int id, CancellationToken ct)
    {
        var part = await db.EquipmentParts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (part is null) return NotFound();

        db.EquipmentParts.Remove(part);
        await db.SaveChangesAsync(ct);

        return Saved("Part deleted.");
    }

    // ----- Maintenance --------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public IActionResult MaintenanceForm(int equipmentId)
        => PartialView("_MaintenanceForm", new MaintenanceFormViewModel
        {
            EquipmentId = equipmentId,
            MaintenanceDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMaintenance(MaintenanceFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_MaintenanceForm", model);
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var equipment = await db.Equipments.FirstOrDefaultAsync(e => e.Id == model.EquipmentId, ct);
        if (equipment is null) return NotFound();

        var record = new EquipmentMaintenance
        {
            EquipmentId = model.EquipmentId,
            MaintenanceDate = model.MaintenanceDate,
            MaintenanceType = model.MaintenanceType,
            Description = model.Description,
            Cost = model.Cost,
            PerformedBy = model.PerformedBy,
            NextDueDate = model.NextDueDate,
            ConditionAfter = model.ConditionAfter,
            Notes = model.Notes,
        };

        db.EquipmentMaintenances.Add(record);

        // A service usually changes the machine's condition; record that in one step.
        if (model.ConditionAfter is { } condition)
            equipment.Condition = condition;

        await db.SaveChangesAsync(ct);

        // Upkeep is a real cost, so it goes through the same books as everything else.
        if (model.Cost > 0)
        {
            var category = await db.ExpenseCategories
                .FirstAsync(c => c.Name == ExpenseCategoryNames.Maintenance, ct);

            db.Expenses.Add(new Expense
            {
                ExpenseCategoryId = category.Id,
                ExpenseDate = model.MaintenanceDate,
                Amount = model.Cost,
                PaymentMethod = PaymentMethod.Cash,
                Description = $"{model.MaintenanceType} on {equipment.Name}",
                ReferenceType = ReferenceType.Maintenance,
                ReferenceId = record.Id,
                IsSystemGenerated = true,
            });

            await db.SaveChangesAsync(ct);

            await cashBook.PostAsync(
                model.MaintenanceDate, CashDirection.Out, model.Cost,
                ExpenseCategoryNames.Maintenance, PaymentMethod.Cash,
                $"{model.MaintenanceType} on {equipment.Name}",
                ReferenceType.Maintenance, record.Id, ct: ct);
        }

        await transaction.CommitAsync(ct);
        return Saved("Maintenance recorded.");
    }

    // ----- Helpers ------------------------------------------------------------------------

    private async Task FillOptionsAsync(EquipmentFormViewModel model, CancellationToken ct)
    {
        model.Suppliers = await db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new SelectOption(s.Id, s.Name)).ToListAsync(ct);

        model.Ponds = await db.Ponds.AsNoTracking()
            .OrderBy(p => p.Name).Select(p => new SelectOption(p.Id, p.Name)).ToListAsync(ct);
    }
}
