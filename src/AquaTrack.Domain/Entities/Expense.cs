using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>
/// A cost line. Expenses raised automatically by purchases/usages carry a
/// <see cref="ReferenceType"/>/<see cref="ReferenceId"/> back to their source document.
/// </summary>
public class Expense : BaseEntity
{
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }

    public DateOnly ExpenseDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Description { get; set; }
    public string? VoucherNo { get; set; }

    public int? PondId { get; set; }
    public Pond? Pond { get; set; }

    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;
    public int? ReferenceId { get; set; }

    /// <summary>System-generated expenses are read-only in the UI; edit the source document instead.</summary>
    public bool IsSystemGenerated { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
