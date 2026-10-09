using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Services;

public interface IExpenseService
{
    /// <summary>Records a manual expense and the matching cash-out entry.</summary>
    Task<Expense> CreateAsync(Expense expense, CancellationToken ct = default);

    Task UpdateAsync(Expense expense, CancellationToken ct = default);
    Task DeleteAsync(int expenseId, CancellationToken ct = default);

    /// <summary>Records income that is not a fish sale, plus the matching cash-in entry.</summary>
    Task<OtherIncome> CreateIncomeAsync(OtherIncome income, CancellationToken ct = default);

    Task DeleteIncomeAsync(int incomeId, CancellationToken ct = default);
}

public class ExpenseService(IApplicationDbContext db, ICashBookService cashBook) : IExpenseService
{
    public async Task<Expense> CreateAsync(Expense expense, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        expense.IsSystemGenerated = false;
        expense.ReferenceType = ReferenceType.Expense;

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        var categoryName = await db.ExpenseCategories
            .Where(c => c.Id == expense.ExpenseCategoryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? "Expense";

        await cashBook.PostAsync(
            expense.ExpenseDate, CashDirection.Out, expense.Amount,
            categoryName, expense.PaymentMethod,
            expense.Description,
            ReferenceType.Expense, expense.Id, ct: ct);

        await transaction.CommitAsync(ct);
        return expense;
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var existing = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expense.Id, ct)
                       ?? throw new InvalidOperationException($"Expense {expense.Id} not found.");

        if (existing.IsSystemGenerated)
            throw new InvalidOperationException("This expense was created by another document. Edit that document instead.");

        existing.ExpenseCategoryId = expense.ExpenseCategoryId;
        existing.ExpenseDate = expense.ExpenseDate;
        existing.Amount = expense.Amount;
        existing.PaymentMethod = expense.PaymentMethod;
        existing.Description = expense.Description;
        existing.VoucherNo = expense.VoucherNo;
        existing.PondId = expense.PondId;

        await db.SaveChangesAsync(ct);

        // Rewrite the ledger entry rather than patching it, so the two can never drift apart.
        await cashBook.ReverseAsync(ReferenceType.Expense, existing.Id, ct);

        var categoryName = await db.ExpenseCategories
            .Where(c => c.Id == existing.ExpenseCategoryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? "Expense";

        await cashBook.PostAsync(
            existing.ExpenseDate, CashDirection.Out, existing.Amount,
            categoryName, existing.PaymentMethod,
            existing.Description,
            ReferenceType.Expense, existing.Id, ct: ct);

        await transaction.CommitAsync(ct);
    }

    public async Task DeleteAsync(int expenseId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId, ct)
                      ?? throw new InvalidOperationException($"Expense {expenseId} not found.");

        if (expense.IsSystemGenerated)
            throw new InvalidOperationException("This expense was created by another document. Delete that document instead.");

        await cashBook.ReverseAsync(ReferenceType.Expense, expenseId, ct);

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    public async Task<OtherIncome> CreateIncomeAsync(OtherIncome income, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        db.OtherIncomes.Add(income);
        await db.SaveChangesAsync(ct);

        await cashBook.PostAsync(
            income.IncomeDate, CashDirection.In, income.Amount,
            "Other income", income.PaymentMethod,
            income.Source,
            ReferenceType.OtherIncome, income.Id, ct: ct);

        await transaction.CommitAsync(ct);
        return income;
    }

    public async Task DeleteIncomeAsync(int incomeId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var income = await db.OtherIncomes.FirstOrDefaultAsync(i => i.Id == incomeId, ct)
                     ?? throw new InvalidOperationException($"Income {incomeId} not found.");

        await cashBook.ReverseAsync(ReferenceType.OtherIncome, incomeId, ct);

        db.OtherIncomes.Remove(income);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
