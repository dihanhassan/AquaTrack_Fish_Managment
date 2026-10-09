using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class FeedPurchase : BaseEntity
{
    public int FeedItemId { get; set; }
    public FeedItem? FeedItem { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal PaidAmount { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Notes { get; set; }

    public decimal DueAmount => TotalPrice - PaidAmount;
}
