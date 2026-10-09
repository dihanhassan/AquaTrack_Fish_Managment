using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class FishMortality : BaseEntity
{
    public int FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public DateOnly RecordDate { get; set; }
    public int Quantity { get; set; }
    public MortalityCause Cause { get; set; } = MortalityCause.Unknown;
    public decimal? AvgWeightGram { get; set; }
    public string? Notes { get; set; }

    /// <summary>Value of the loss at the batch's stocking cost — feeds the loss column in reports.</summary>
    public decimal EstimatedLoss { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
