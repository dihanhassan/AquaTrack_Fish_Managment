using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

/// <summary>One row of the pond list, projected in SQL so the page never loads whole aggregates.</summary>
public class PondRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public decimal AreaDecimal { get; init; }
    public decimal DepthMetre { get; init; }
    public WaterType WaterType { get; init; }
    public PondStatus Status { get; init; }
    public string? Location { get; init; }

    public int ActiveBatches { get; init; }
    public int FishCount { get; init; }
    public decimal BiomassGram { get; init; }

    public decimal BiomassKg => Math.Round(BiomassGram / 1000m, 2);

    /// <summary>Fish per decimal of surface area, the density figure farmers actually use.</summary>
    public decimal StockingDensity => AreaDecimal <= 0 ? 0 : Math.Round(FishCount / AreaDecimal, 1);
}

public class PondListViewModel
{
    public required PagedList<PondRow> Page { get; init; }
    public required ListQuery Query { get; init; }
    public string? StatusFilter { get; init; }

    /// <summary>Filter state carried onto every sort and page link so nothing is lost on navigation.</summary>
    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (!string.IsNullOrEmpty(StatusFilter)) values["status"] = StatusFilter;
        return values;
    }
}

public class PondFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter a pond name.")]
    [StringLength(120)]
    [Display(Name = "Pond name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Code")]
    public string? Code { get; set; }

    [Range(0.01, 100000, ErrorMessage = "Enter the area in decimals.")]
    [Display(Name = "Area (decimal)")]
    public decimal AreaDecimal { get; set; }

    [Range(0.1, 100, ErrorMessage = "Enter the average depth in metres.")]
    [Display(Name = "Depth (m)")]
    public decimal DepthMetre { get; set; } = 1.5m;

    [Display(Name = "Water type")]
    public WaterType WaterType { get; set; } = WaterType.Freshwater;

    [Display(Name = "Status")]
    public PondStatus Status { get; set; } = PondStatus.Active;

    [StringLength(250)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [Display(Name = "Commissioned on")]
    public DateOnly? CommissionedOn { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public bool IsEdit => Id.HasValue;
}

public class PondDetailsViewModel
{
    public required Pond Pond { get; init; }
    public IReadOnlyList<FishStock> Batches { get; init; } = [];
    public IReadOnlyList<WaterQualityLog> RecentWaterQuality { get; init; } = [];

    public decimal FeedThisMonth { get; init; }
    public decimal FeedCostThisMonth { get; init; }
    public decimal MedicineCostThisMonth { get; init; }

    public IEnumerable<FishStock> ActiveBatches => Batches.Where(b => b.Status == StockStatus.Active);
    public int TotalFish => ActiveBatches.Sum(b => b.CurrentQuantity);
    public decimal TotalBiomassKg => Math.Round(ActiveBatches.Sum(b => b.BiomassKg), 2);
}
