using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>A sold lot of fish, priced per kg and optionally traced back to the pond it came from.</summary>
public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int FishTypeId { get; set; }
    public FishType? FishType { get; set; }

    public int? PondId { get; set; }
    public Pond? Pond { get; set; }

    public int? FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public decimal QuantityKg { get; set; }
    public int? PieceCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
