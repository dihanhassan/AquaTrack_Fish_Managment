using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>A spare part held for a machine — tracked separately so replacement history stays auditable.</summary>
public class EquipmentPart : BaseEntity
{
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? PartNo { get; set; }
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Piece;
    public decimal Quantity { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal UnitPrice { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public string? Notes { get; set; }

    public decimal StockValue => Quantity * UnitPrice;
    public bool IsLowStock => ReorderLevel > 0 && Quantity <= ReorderLevel;
}
