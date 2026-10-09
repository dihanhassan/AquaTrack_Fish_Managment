using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>Fish taken out of a pond. May or may not be sold immediately — sales reference it separately.</summary>
public class Harvest : BaseEntity
{
    public int FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public DateOnly HarvestDate { get; set; }
    public int Quantity { get; set; }
    public decimal TotalWeightKg { get; set; }
    public decimal? ExpectedUnitPrice { get; set; }
    public bool IsPartial { get; set; } = true;
    public string? Notes { get; set; }

    public decimal AvgWeightGram => Quantity == 0 ? 0 : TotalWeightKg * 1000m / Quantity;
}
