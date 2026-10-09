using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>A single culture unit. Everything operational (stock, feed, medicine, water) hangs off a pond.</summary>
public class Pond : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    /// <summary>Surface area in decimals (1 decimal = 40.46 m²), the unit Bangladeshi farms actually use.</summary>
    public decimal AreaDecimal { get; set; }
    public decimal DepthMetre { get; set; }
    public WaterType WaterType { get; set; } = WaterType.Freshwater;
    public PondStatus Status { get; set; } = PondStatus.Active;

    public string? Location { get; set; }
    public DateOnly? CommissionedOn { get; set; }
    public string? Notes { get; set; }

    public ICollection<FishStock> FishStocks { get; set; } = [];
    public ICollection<FeedUsage> FeedUsages { get; set; } = [];
    public ICollection<MedicineUsage> MedicineUsages { get; set; } = [];
    public ICollection<WaterQualityLog> WaterQualityLogs { get; set; } = [];

    /// <summary>Volume in cubic metres, used by stocking-density and feed-rate calculations.</summary>
    public decimal VolumeCubicMetre => AreaDecimal * 40.46m * DepthMetre;
}
