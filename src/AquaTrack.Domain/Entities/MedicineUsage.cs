using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>A treatment applied to a pond — the medicine-side mirror of <see cref="FeedUsage"/>.</summary>
public class MedicineUsage : BaseEntity
{
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public int PondId { get; set; }
    public Pond? Pond { get; set; }

    public DateOnly UsageDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
