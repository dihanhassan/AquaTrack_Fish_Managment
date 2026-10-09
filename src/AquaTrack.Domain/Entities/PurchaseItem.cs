using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>
/// One line of a purchase. <see cref="ItemType"/> plus the matching nullable FK tells the posting
/// service which inventory table to increment.
/// </summary>
public class PurchaseItem : BaseEntity
{
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public PurchaseItemType ItemType { get; set; }
    public int? FeedItemId { get; set; }
    public FeedItem? FeedItem { get; set; }
    public int? MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int? FishTypeId { get; set; }
    public FishType? FishType { get; set; }

    /// <summary>Free-text label used when the line is "Other" and points at no inventory record.</summary>
    public string Description { get; set; } = string.Empty;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
