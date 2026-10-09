using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>A cultured species (Rui, Katla, Tilapia…) plus the growth constants the AI module needs.</summary>
public class FishType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LocalName { get; set; }
    public string? ScientificName { get; set; }
    public string? Description { get; set; }

    /// <summary>Typical weight at harvest in grams — the default target for harvest advice.</summary>
    public decimal? TargetWeightGram { get; set; }
    /// <summary>Expected days from stocking to target weight, used to sanity-check growth predictions.</summary>
    public int? GrowOutDays { get; set; }
    /// <summary>Recommended daily feed as a percentage of body weight.</summary>
    public decimal DefaultFeedRatePercent { get; set; } = 3m;

    public ICollection<FishStock> FishStocks { get; set; } = [];
}
