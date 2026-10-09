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
/// Medicine inventory. Mirrors the feed module, with expiry tracking on top because an expired
/// treatment is both a financial write-off and a fish-safety issue.
/// </summary>
public class MedicinesController(
    IApplicationDbContext db,
    IStockService stock,
    ICashBookService cashBook) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Medicine, object>>> Sortable = new()
    {
        ["name"] = m => m.Name,
        ["stock"] = m => m.CurrentStock,
        ["expiry"] = m => m.ExpiryDate!,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Medicine";
        ViewData["Subtitle"] = "Treatments, stock levels and expiry";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<MedicineListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var medicines = db.Medicines.Include(m => m.Supplier).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            medicines = medicines.Where(m => m.Name.Contains(term) || (m.Brand != null && m.Brand.Contains(term)));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var filter = Request.Query["filter"].ToString();

        medicines = filter switch
        {
            "low" => medicines.Where(m => m.ReorderLevel > 0 && m.CurrentStock <= m.ReorderLevel),
            "expiring" => medicines.Where(m => m.ExpiryDate != null && m.ExpiryDate <= today.AddDays(30)),
            "expired" => medicines.Where(m => m.ExpiryDate != null && m.ExpiryDate < today),
            _ => medicines,
        };

        var sorted = medicines.ApplySort(query.Sort, query.Desc, Sortable, "name");

        return new MedicineListViewModel
        {
            Page = await PagedList<Medicine>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            Filter = filter,
        };
    }

    // ----- Medicine -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        var model = new MedicineFormViewModel();

        if (id is { } medicineId)
        {
            var medicine = await db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == medicineId, ct);
            if (medicine is null) return NotFound();

            model = new MedicineFormViewModel
            {
                Id = medicine.Id,
                Name = medicine.Name,
                Brand = medicine.Brand,
                MedicineType = medicine.MedicineType,
                Unit = medicine.Unit,
                SupplierId = medicine.SupplierId,
                ReorderLevel = medicine.ReorderLevel,
                ExpiryDate = medicine.ExpiryDate,
                Dosage = medicine.Dosage,
                IsActive = medicine.IsActive,
            };
        }

        model.Suppliers = await SupplierOptionsAsync(ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MedicineFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Suppliers = await SupplierOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        Medicine medicine;
        if (model.Id is { } id)
        {
            medicine = await db.Medicines.FirstOrDefaultAsync(m => m.Id == id, ct)
                       ?? throw new InvalidOperationException($"Medicine {id} not found.");
        }
        else
        {
            medicine = new Medicine();
            db.Medicines.Add(medicine);
        }

        medicine.Name = model.Name.Trim();
        medicine.Brand = model.Brand;
        medicine.MedicineType = model.MedicineType;
        medicine.Unit = model.Unit;
        medicine.SupplierId = model.SupplierId == 0 ? null : model.SupplierId;
        medicine.ReorderLevel = model.ReorderLevel;
        medicine.ExpiryDate = model.ExpiryDate;
        medicine.Dosage = model.Dosage;
        medicine.IsActive = model.IsActive;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"Medicine \"{medicine.Name}\" added." : "Medicine updated.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var medicine = await db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (medicine is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete medicine",
            Message = $"Delete \"{medicine.Name}\"?",
            Detail = medicine.CurrentStock > 0
                ? $"There is still {medicine.CurrentStock:N2} {medicine.Unit} in stock."
                : "Purchase and treatment history is kept for reporting.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var medicine = await db.Medicines.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (medicine is null) return NotFound();

        if (medicine.CurrentStock > 0)
            return Failed("Use or write off the remaining stock before deleting this medicine.");

        db.Medicines.Remove(medicine);
        await db.SaveChangesAsync(ct);

        return Saved($"Medicine \"{medicine.Name}\" deleted.");
    }

    // ----- Purchase -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageFarm)]
    public async Task<IActionResult> PurchaseForm(int? medicineId, CancellationToken ct)
        => PartialView("_PurchaseForm", new MedicinePurchaseFormViewModel
        {
            MedicineId = medicineId ?? 0,
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            Medicines = await MedicineOptionsAsync(ct),
            Suppliers = await SupplierOptionsAsync(ct),
        });

    [HttpPost]
    [Authorize(Policy = Policies.ManageFarm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePurchase(MedicinePurchaseFormViewModel model, CancellationToken ct)
    {
        if (model.PaidAmount > model.TotalPrice)
            ModelState.AddModelError(nameof(model.PaidAmount), "Paid amount cannot exceed the total.");

        if (!ModelState.IsValid)
        {
            model.Medicines = await MedicineOptionsAsync(ct);
            model.Suppliers = await SupplierOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_PurchaseForm", model);
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var purchase = new MedicinePurchase
        {
            MedicineId = model.MedicineId,
            SupplierId = model.SupplierId == 0 ? null : model.SupplierId,
            PurchaseDate = model.PurchaseDate,
            Quantity = model.Quantity,
            UnitPrice = model.UnitPrice,
            TotalPrice = model.TotalPrice,
            PaymentMethod = model.PaymentMethod,
            PaidAmount = model.PaidAmount,
            ExpiryDate = model.ExpiryDate,
            BatchNo = model.BatchNo,
            Notes = model.Notes,
        };

        db.MedicinePurchases.Add(purchase);
        await db.SaveChangesAsync(ct);

        await stock.ReceiveMedicineAsync(model.MedicineId, model.Quantity, model.UnitPrice, ct);

        // A newer batch sets the expiry the alerts watch.
        if (model.ExpiryDate is { } expiry)
        {
            var medicine = await db.Medicines.FirstAsync(m => m.Id == model.MedicineId, ct);
            if (medicine.ExpiryDate is null || expiry > medicine.ExpiryDate)
                medicine.ExpiryDate = expiry;
        }

        var category = await db.ExpenseCategories.FirstAsync(c => c.Name == ExpenseCategoryNames.Medicine, ct);

        db.Expenses.Add(new Expense
        {
            ExpenseCategoryId = category.Id,
            ExpenseDate = model.PurchaseDate,
            Amount = model.TotalPrice,
            PaymentMethod = model.PaymentMethod,
            Description = $"Medicine purchase: {model.Quantity:N2} units",
            VoucherNo = model.BatchNo,
            ReferenceType = ReferenceType.MedicinePurchase,
            ReferenceId = purchase.Id,
            IsSystemGenerated = true,
        });

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
                ExpenseCategoryNames.Medicine, model.PaymentMethod,
                $"Medicine purchase {model.BatchNo}".Trim(),
                ReferenceType.MedicinePurchase, purchase.Id,
                supplierId: purchase.SupplierId, ct: ct);
        }

        await transaction.CommitAsync(ct);
        return Saved("Medicine purchase recorded.");
    }

    // ----- Usage --------------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.RecordDailyData)]
    public async Task<IActionResult> UsageForm(int? medicineId, CancellationToken ct)
        => PartialView("_UsageForm", new MedicineUsageFormViewModel
        {
            MedicineId = medicineId ?? 0,
            UsageDate = DateOnly.FromDateTime(DateTime.Today),
            Medicines = await MedicineOptionsAsync(ct),
            Ponds = await PondOptionsAsync(ct),
        });

    [HttpPost]
    [Authorize(Policy = Policies.RecordDailyData)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUsage(MedicineUsageFormViewModel model, CancellationToken ct)
    {
        var medicine = await db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == model.MedicineId, ct);

        if (medicine is not null && model.Quantity > medicine.CurrentStock)
            ModelState.AddModelError(nameof(model.Quantity),
                $"Only {medicine.CurrentStock:N2} {medicine.Unit} of {medicine.Name} is in stock.");

        if (medicine is { IsExpired: true })
            ModelState.AddModelError(string.Empty,
                $"{medicine.Name} expired on {medicine.ExpiryDate:dd MMM yyyy} and should not be used.");

        if (!ModelState.IsValid)
        {
            model.Medicines = await MedicineOptionsAsync(ct);
            model.Ponds = await PondOptionsAsync(ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_UsageForm", model);
        }

        await using var transaction = await db.BeginTransactionAsync(ct);

        var unitCost = await stock.IssueMedicineAsync(model.MedicineId, model.Quantity, ct);

        db.MedicineUsages.Add(new MedicineUsage
        {
            MedicineId = model.MedicineId,
            PondId = model.PondId,
            UsageDate = model.UsageDate,
            Quantity = model.Quantity,
            UnitCost = unitCost,
            TotalCost = Math.Round(model.Quantity * unitCost, 2),
            Purpose = model.Purpose,
            Notes = model.Notes,
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Saved("Treatment recorded.");
    }

    // ----- Detail -------------------------------------------------------------------------

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var medicine = await db.Medicines.Include(m => m.Supplier)
            .AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

        if (medicine is null) return NotFound();

        var model = new MedicineDetailsViewModel
        {
            Medicine = medicine,
            Purchases = await db.MedicinePurchases.Include(p => p.Supplier)
                .Where(p => p.MedicineId == id)
                .OrderByDescending(p => p.PurchaseDate).Take(25)
                .AsNoTracking().ToListAsync(ct),
            Usages = await db.MedicineUsages.Include(u => u.Pond)
                .Where(u => u.MedicineId == id)
                .OrderByDescending(u => u.UsageDate).Take(25)
                .AsNoTracking().ToListAsync(ct),
        };

        ViewData["Title"] = medicine.Name;
        ViewData["Subtitle"] = $"{medicine.CurrentStock:N2} {medicine.Unit} in stock";
        return View(model);
    }

    // ----- Helpers ------------------------------------------------------------------------

    private async Task<IReadOnlyList<SelectOption>> SupplierOptionsAsync(CancellationToken ct)
        => await db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new SelectOption(s.Id, s.Name)).ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOption>> MedicineOptionsAsync(CancellationToken ct)
        => await db.Medicines.AsNoTracking().Where(m => m.IsActive)
            .OrderBy(m => m.Name).Select(m => new SelectOption(m.Id, m.Name)).ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOption>> PondOptionsAsync(CancellationToken ct)
        => await db.Ponds.AsNoTracking().Where(p => p.Status == PondStatus.Active)
            .OrderBy(p => p.Name).Select(p => new SelectOption(p.Id, p.Name)).ToListAsync(ct);
}
