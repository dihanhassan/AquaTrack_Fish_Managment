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
/// Costs and non-sale income. Expenses raised automatically by a purchase or a usage are read-only
/// here, because editing them would put this list and its source document out of step.
/// </summary>
[Authorize(Policy = Policies.ManageFarm)]
public class ExpensesController(IApplicationDbContext db, IExpenseService expenses) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Expense, object>>> Sortable = new()
    {
        ["date"] = e => e.ExpenseDate,
        ["category"] = e => e.ExpenseCategory!.Name,
        ["amount"] = e => e.Amount,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Expenses";
        ViewData["Subtitle"] = "Where the money goes";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<ExpenseListViewModel> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = ParseDate(Request.Query["from"]) ?? new DateOnly(today.Year, today.Month, 1);
        var to = ParseDate(Request.Query["to"]) ?? today;

        var rows = db.Expenses
            .Include(e => e.ExpenseCategory)
            .Include(e => e.Pond)
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(e =>
                (e.Description != null && e.Description.Contains(term)) ||
                (e.VoucherNo != null && e.VoucherNo.Contains(term)) ||
                e.ExpenseCategory!.Name.Contains(term));
        }

        if (int.TryParse(Request.Query["categoryId"], out var categoryId) && categoryId > 0)
            rows = rows.Where(e => e.ExpenseCategoryId == categoryId);

        var sorted = rows.ApplySort(query.Sort, query.Desc, Sortable, "date");
        if (string.IsNullOrWhiteSpace(query.Sort)) sorted = sorted.OrderByDescending(e => e.ExpenseDate);

        // The split by category is what makes this screen answer "where is the money going".
        var byCategory = await rows
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new { Label = g.Key, Value = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Value)
            .ToListAsync(ct);

        return new ExpenseListViewModel
        {
            Page = await PagedList<Expense>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            From = from,
            To = to,
            CategoryId = categoryId > 0 ? categoryId : null,
            Categories = await CategoryOptionsAsync(ct),
            PeriodTotal = await rows.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m,
            ByCategory = [.. byCategory.Select(c => new CategoryTotal(c.Label, c.Value))],
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        var model = new ExpenseFormViewModel { ExpenseDate = DateOnly.FromDateTime(DateTime.Today) };

        if (id is { } expenseId)
        {
            var expense = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == expenseId, ct);
            if (expense is null) return NotFound();

            if (expense.IsSystemGenerated)
                return Failed("This expense came from another document. Edit that document instead.");

            model = new ExpenseFormViewModel
            {
                Id = expense.Id,
                ExpenseCategoryId = expense.ExpenseCategoryId,
                ExpenseDate = expense.ExpenseDate,
                Amount = expense.Amount,
                PaymentMethod = expense.PaymentMethod,
                Description = expense.Description,
                VoucherNo = expense.VoucherNo,
                PondId = expense.PondId,
            };
        }

        await FillOptionsAsync(model, ct);
        return PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ExpenseFormViewModel model, CancellationToken ct)
    {
        if (model.ExpenseDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.ExpenseDate), "An expense cannot be dated in the future.");

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        var expense = new Expense
        {
            Id = model.Id ?? 0,
            ExpenseCategoryId = model.ExpenseCategoryId,
            ExpenseDate = model.ExpenseDate,
            Amount = model.Amount,
            PaymentMethod = model.PaymentMethod,
            Description = model.Description,
            VoucherNo = model.VoucherNo,
            PondId = model.PondId == 0 ? null : model.PondId,
        };

        try
        {
            if (model.Id is null)
                await expenses.CreateAsync(expense, ct);
            else
                await expenses.UpdateAsync(expense, ct);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await FillOptionsAsync(model, ct);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        return Saved(model.Id is null ? $"Expense of {model.Amount:N2} recorded." : "Expense updated.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var expense = await db.Expenses.Include(e => e.ExpenseCategory)
            .AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

        if (expense is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete expense",
            Message = $"Delete the {expense.Amount:N2} {expense.ExpenseCategory?.Name} expense?",
            Detail = "Its cash-book entry is removed with it.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await expenses.DeleteAsync(id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(ex.Message);
        }

        return Saved("Expense deleted.");
    }

    // ----- Other income -------------------------------------------------------------------

    [HttpGet]
    public IActionResult IncomeForm()
        => PartialView("_IncomeForm", new OtherIncomeFormViewModel
        {
            IncomeDate = DateOnly.FromDateTime(DateTime.Today),
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIncome(OtherIncomeFormViewModel model, CancellationToken ct)
    {
        if (model.IncomeDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.IncomeDate), "Income cannot be dated in the future.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_IncomeForm", model);
        }

        await expenses.CreateIncomeAsync(new OtherIncome
        {
            IncomeDate = model.IncomeDate,
            Source = model.Source.Trim(),
            Amount = model.Amount,
            PaymentMethod = model.PaymentMethod,
            Description = model.Description,
        }, ct);

        return Saved($"Income of {model.Amount:N2} recorded.");
    }

    // ----- Categories ---------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        ViewData["Title"] = "Expense categories";
        ViewData["Subtitle"] = "How costs are grouped on reports";

        return View(await db.ExpenseCategories.AsNoTracking()
            .OrderByDescending(c => c.IsSystem).ThenBy(c => c.Name)
            .ToListAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> CategoryForm(int? id, CancellationToken ct)
    {
        if (id is null) return PartialView("_CategoryForm", new ExpenseCategoryFormViewModel());

        var category = await db.ExpenseCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();

        return PartialView("_CategoryForm", new ExpenseCategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            IsSystem = category.IsSystem,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(ExpenseCategoryFormViewModel model, CancellationToken ct)
    {
        var nameTaken = await db.ExpenseCategories
            .AnyAsync(c => c.Name == model.Name && c.Id != model.Id, ct);

        if (nameTaken)
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_CategoryForm", model);
        }

        ExpenseCategory category;
        if (model.Id is { } id)
        {
            category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new InvalidOperationException($"Category {id} not found.");
        }
        else
        {
            category = new ExpenseCategory();
            db.ExpenseCategories.Add(category);
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description;
        category.IsActive = model.IsActive;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? "Category added." : "Category updated.");
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();

        // Seeded categories are referenced by the automatic posting rules.
        if (category.IsSystem)
            return Failed("This is a built-in category and cannot be deleted. Deactivate it instead.");

        var inUse = await db.Expenses.AnyAsync(e => e.ExpenseCategoryId == id, ct);
        if (inUse)
            return Failed("This category has expenses against it. Deactivate it instead.");

        db.ExpenseCategories.Remove(category);
        await db.SaveChangesAsync(ct);

        return Saved("Category deleted.");
    }

    // ----- Helpers ------------------------------------------------------------------------

    private async Task FillOptionsAsync(ExpenseFormViewModel model, CancellationToken ct)
    {
        model.Categories = await CategoryOptionsAsync(ct);
        model.Ponds = await db.Ponds.AsNoTracking()
            .OrderBy(p => p.Name).Select(p => new SelectOption(p.Id, p.Name)).ToListAsync(ct);
    }

    private async Task<IReadOnlyList<SelectOption>> CategoryOptionsAsync(CancellationToken ct)
        => await db.ExpenseCategories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.Name).Select(c => new SelectOption(c.Id, c.Name)).ToListAsync(ct);

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParse(value, out var date) ? date : null;
}
