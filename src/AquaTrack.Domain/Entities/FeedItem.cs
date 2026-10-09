using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>A feed SKU held in the store. Stock is moved by purchases (in) and usages (out).</summary>
public class FeedItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public FeedType FeedType { get; set; } = FeedType.Floating;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal CurrentStock { get; set; }
    /// <summary>Stock level at or below which the dashboard raises a low-stock alert.</summary>
    public decimal ReorderLevel { get; set; }
    /// <summary>Weighted-average cost per unit, recalculated on each purchase; used to cost every usage.</summary>
    public decimal AverageUnitCost { get; set; }
    public decimal LastUnitPrice { get; set; }
    public decimal? ProteinPercent { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FeedPurchase> Purchases { get; set; } = [];
    public ICollection<FeedUsage> Usages { get; set; } = [];

    public decimal StockValue => CurrentStock * AverageUnitCost;
    public bool IsLowStock => ReorderLevel > 0 && CurrentStock <= ReorderLevel;
}
