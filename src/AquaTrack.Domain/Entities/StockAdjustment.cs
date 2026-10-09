using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>Manual correction to a batch's head count (physical count, transfer, data fix).</summary>
public class StockAdjustment : BaseEntity
{
    public int FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public DateOnly AdjustmentDate { get; set; }
    /// <summary>Signed delta: negative removes fish, positive adds them.</summary>
    public int QuantityDelta { get; set; }
    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.PhysicalCount;
    public string? Notes { get; set; }
}
