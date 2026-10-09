using System.ComponentModel.DataAnnotations;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class FishTypeRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? LocalName { get; init; }
    public string? ScientificName { get; init; }
    public decimal? TargetWeightGram { get; init; }
    public int? GrowOutDays { get; init; }
    public decimal DefaultFeedRatePercent { get; init; }
    public int ActiveBatches { get; init; }

    /// <summary>Average daily gain implied by the target weight and grow-out period.</summary>
    public decimal? ExpectedDailyGainGram => TargetWeightGram is { } target && GrowOutDays is > 0
        ? Math.Round(target / GrowOutDays.Value, 2)
        : null;
}

public class FishTypeListViewModel
{
    public required PagedList<FishTypeRow> Page { get; init; }
    public required ListQuery Query { get; init; }
}

public class FishTypeFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the species name.")]
    [StringLength(120)]
    [Display(Name = "Species name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Local name")]
    public string? LocalName { get; set; }

    [StringLength(150)]
    [Display(Name = "Scientific name")]
    public string? ScientificName { get; set; }

    [Range(1, 100000, ErrorMessage = "Enter the usual harvest weight in grams.")]
    [Display(Name = "Target harvest weight (g)")]
    public decimal? TargetWeightGram { get; set; }

    [Range(1, 2000, ErrorMessage = "Enter the usual grow-out period in days.")]
    [Display(Name = "Grow-out period (days)")]
    public int? GrowOutDays { get; set; }

    [Range(0.1, 20, ErrorMessage = "Enter a daily feed rate between 0.1% and 20%.")]
    [Display(Name = "Daily feed rate (% of body weight)")]
    public decimal DefaultFeedRatePercent { get; set; } = 3m;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsEdit => Id.HasValue;
}
