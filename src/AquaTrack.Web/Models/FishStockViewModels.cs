using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

/// <summary>A dropdown choice. Shared by every form that picks a pond, species, supplier or customer.</summary>
public record SelectOption(int Id, string Name);

public class FishStockListViewModel
{
    public required PagedList<FishStock> Page { get; init; }
    public required ListQuery Query { get; init; }
    public int? PondId { get; init; }
    public string? StatusFilter { get; init; }
    public IReadOnlyList<SelectOption> Ponds { get; init; } = [];

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (PondId is { } pond) values["pondId"] = pond.ToString();
        if (!string.IsNullOrEmpty(StatusFilter)) values["status"] = StatusFilter;
        return values;
    }
}

public class FishStockFormViewModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a pond.")]
    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a species.")]
    [Display(Name = "Species")]
    public int FishTypeId { get; set; }

    [StringLength(60)]
    [Display(Name = "Batch code")]
    public string? BatchCode { get; set; }

    [Display(Name = "Stocking date")]
    public DateOnly StockDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 10_000_000, ErrorMessage = "Enter how many fish were stocked.")]
    [Display(Name = "Quantity (pcs)")]
    public int InitialQuantity { get; set; }

    [Range(0.01, 100000, ErrorMessage = "Enter the average weight at stocking.")]
    [Display(Name = "Avg weight (g)")]
    public decimal InitialAvgWeightGram { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter the cost per fish.")]
    [Display(Name = "Cost per fish")]
    public decimal UnitCost { get; set; }

    [StringLength(200)]
    [Display(Name = "Source / hatchery")]
    public string? Source { get; set; }

    [Display(Name = "Status")]
    public StockStatus Status { get; set; } = StockStatus.Active;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];
    public IReadOnlyList<SelectOption> Species { get; set; } = [];

    public bool IsEdit => Id.HasValue;

    /// <summary>Shown live on the form so the user can sanity-check the batch before saving.</summary>
    public decimal TotalCost => InitialQuantity * UnitCost;
}

public class FishStockDetailsViewModel
{
    public required FishStock Batch { get; init; }
    public IReadOnlyList<FishGrowth> GrowthRecords { get; init; } = [];
    public IReadOnlyList<FishMortality> MortalityRecords { get; init; } = [];
    public IReadOnlyList<Harvest> Harvests { get; init; } = [];
    public IReadOnlyList<StockAdjustment> Adjustments { get; init; } = [];

    public int TotalDead => MortalityRecords.Sum(m => m.Quantity);
    public int TotalHarvested => Harvests.Sum(h => h.Quantity);
    public decimal HarvestedKg => Harvests.Sum(h => h.TotalWeightKg);

    public decimal SurvivalPercent => Batch.InitialQuantity == 0
        ? 0
        : Math.Round((decimal)(Batch.InitialQuantity - TotalDead) / Batch.InitialQuantity * 100, 1);

    /// <summary>Growth samples oldest-first, which is the order a trend chart needs.</summary>
    public IEnumerable<FishGrowth> GrowthTrend => GrowthRecords.OrderBy(g => g.RecordDate);
}

public class GrowthFormViewModel
{
    public int FishStockId { get; set; }

    [Display(Name = "Sample date")]
    public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 1000, ErrorMessage = "How many fish were weighed?")]
    [Display(Name = "Sample size")]
    public int SampleSize { get; set; } = 10;

    [Range(0.01, 100000, ErrorMessage = "Enter the average weight.")]
    [Display(Name = "Average weight (g)")]
    public decimal AvgWeightGram { get; set; }

    [Range(0, 500)]
    [Display(Name = "Average length (cm)")]
    public decimal? AvgLengthCm { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class MortalityFormViewModel
{
    public int FishStockId { get; set; }

    [Display(Name = "Date")]
    public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 10_000_000, ErrorMessage = "Enter how many fish died.")]
    [Display(Name = "Quantity (pcs)")]
    public int Quantity { get; set; }

    [Display(Name = "Likely cause")]
    public MortalityCause Cause { get; set; } = MortalityCause.Unknown;

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class HarvestFormViewModel
{
    public int FishStockId { get; set; }

    [Display(Name = "Harvest date")]
    public DateOnly HarvestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 10_000_000, ErrorMessage = "Enter how many fish were taken.")]
    [Display(Name = "Quantity (pcs)")]
    public int Quantity { get; set; }

    [Range(0.01, 1_000_000, ErrorMessage = "Enter the total weight harvested.")]
    [Display(Name = "Total weight (kg)")]
    public decimal TotalWeightKg { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "Expected price per kg")]
    public decimal? ExpectedUnitPrice { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class AdjustmentFormViewModel
{
    public int FishStockId { get; set; }

    [Display(Name = "Date")]
    public DateOnly AdjustmentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Signed: negative removes fish from the batch, positive adds them.</summary>
    [Display(Name = "Adjustment (+/- pcs)")]
    public int QuantityDelta { get; set; }

    [Display(Name = "Reason")]
    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.PhysicalCount;

    [StringLength(500)]
    public string? Notes { get; set; }
}
