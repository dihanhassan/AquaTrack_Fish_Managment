using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>A purchase document (header). Lines live in <see cref="PurchaseItem"/>.</summary>
public class Purchase : BaseEntity
{
    public string PurchaseNo { get; set; } = string.Empty;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TransportCost { get; set; }
    public decimal GrandTotal { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal PaidAmount { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = [];

    public decimal DueAmount => GrandTotal - PaidAmount;
    public bool IsFullyPaid => DueAmount <= 0;
}
