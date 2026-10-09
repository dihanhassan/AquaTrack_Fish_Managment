using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>Feed issued to a pond on a given day. Costed at the item's average cost at the time of issue.</summary>
public class FeedUsage : BaseEntity
{
    public int FeedItemId { get; set; }
    public FeedItem? FeedItem { get; set; }

    public int PondId { get; set; }
    public Pond? Pond { get; set; }

    public DateOnly UsageDate { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Snapshot of average unit cost so historical cost does not shift when prices change later.</summary>
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
