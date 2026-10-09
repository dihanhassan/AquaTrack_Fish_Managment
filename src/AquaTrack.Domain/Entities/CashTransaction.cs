using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>
/// The single cash book. Every money movement in the system lands here exactly once, which is what
/// makes "Opening + In − Out = Closing" hold for any date range.
/// </summary>
public class CashTransaction : BaseEntity
{
    public DateOnly TransactionDate { get; set; }
    public CashDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? VoucherNo { get; set; }

    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;
    public int? ReferenceId { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public bool IsSystemGenerated { get; set; }

    /// <summary>Signed amount: positive for cash in, negative for cash out. Simplifies balance queries.</summary>
    public decimal SignedAmount => Direction == CashDirection.In ? Amount : -Amount;
}
