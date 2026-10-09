using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

public class ExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Seeded categories (Feed, Medicine, Labour…) cannot be deleted, only renamed.</summary>
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = [];
}
