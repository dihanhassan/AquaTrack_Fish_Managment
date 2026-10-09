using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>One batch of fish stocked into one pond. The central unit for growth, mortality and harvest.</summary>
public class FishStock : BaseEntity
{
    public int PondId { get; set; }
    public Pond? Pond { get; set; }

    public int FishTypeId { get; set; }
    public FishType? FishType { get; set; }

    public string? BatchCode { get; set; }
    public DateOnly StockDate { get; set; }

    public int InitialQuantity { get; set; }
    /// <summary>Kept denormalised and recomputed on every mortality/harvest/adjustment so lists stay cheap.</summary>
    public int CurrentQuantity { get; set; }

    public decimal InitialAvgWeightGram { get; set; }
    /// <summary>Latest sampled average weight; refreshed from the newest <see cref="FishGrowth"/> record.</summary>
    public decimal CurrentAvgWeightGram { get; set; }

    public decimal UnitCost { get; set; }
    public string? Source { get; set; }
    public StockStatus Status { get; set; } = StockStatus.Active;
    public string? Notes { get; set; }

    public ICollection<FishGrowth> GrowthRecords { get; set; } = [];
    public ICollection<FishMortality> MortalityRecords { get; set; } = [];
    public ICollection<Harvest> Harvests { get; set; } = [];
    public ICollection<StockAdjustment> Adjustments { get; set; } = [];

    public decimal TotalStockingCost => InitialQuantity * UnitCost;
    /// <summary>Live biomass in kg — drives feed recommendation and valuation.</summary>
    public decimal BiomassKg => CurrentQuantity * CurrentAvgWeightGram / 1000m;

    /// <summary>
    /// Fish that died, kept denormalised alongside <see cref="CurrentQuantity"/> and recomputed
    /// from the mortality records. Deliberately not derived from initial minus current, because
    /// that difference also contains harvested fish, which are a sale rather than a loss.
    /// </summary>
    public int MortalityCount { get; set; }

    /// <summary>Fish taken out alive and sold, recomputed from the harvest records.</summary>
    public int HarvestedCount { get; set; }

    public decimal MortalityPercent => InitialQuantity == 0
        ? 0
        : Math.Round((decimal)MortalityCount / InitialQuantity * 100, 1);

    public decimal SurvivalPercent => InitialQuantity == 0
        ? 0
        : Math.Round((decimal)(InitialQuantity - MortalityCount) / InitialQuantity * 100, 1);
}
