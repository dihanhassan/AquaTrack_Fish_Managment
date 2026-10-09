using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>A stored output of the AI module, kept so advice can be reviewed and acted on later.</summary>
public class AiRecommendation : BaseEntity
{
    public DateOnly GeneratedOn { get; set; }
    public RecommendationType Type { get; set; }
    public RecommendationStatus Status { get; set; } = RecommendationStatus.New;

    public int? PondId { get; set; }
    public Pond? Pond { get; set; }
    public int? FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The headline number (recommended kg, predicted weight, predicted amount…).</summary>
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    /// <summary>0–1 confidence from the underlying model; low values are shown as "indicative only".</summary>
    public decimal Confidence { get; set; }
    /// <summary>Human-readable explanation of how the number was derived.</summary>
    public string? Rationale { get; set; }
}
