using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>A sampling event: net a few fish, weigh them, record the average.</summary>
public class FishGrowth : BaseEntity
{
    public int FishStockId { get; set; }
    public FishStock? FishStock { get; set; }

    public DateOnly RecordDate { get; set; }
    public int SampleSize { get; set; } = 10;
    public decimal AvgWeightGram { get; set; }
    public decimal? AvgLengthCm { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
