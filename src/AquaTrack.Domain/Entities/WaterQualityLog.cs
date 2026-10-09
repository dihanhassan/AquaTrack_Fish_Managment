using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>Optional water-chemistry reading; the anomaly detector flags out-of-range values.</summary>
public class WaterQualityLog : BaseEntity
{
    public int PondId { get; set; }
    public Pond? Pond { get; set; }

    public DateOnly RecordDate { get; set; }
    public decimal? Ph { get; set; }
    public decimal? TemperatureC { get; set; }
    public decimal? DissolvedOxygenMgL { get; set; }
    public decimal? AmmoniaMgL { get; set; }
    public decimal? NitriteMgL { get; set; }
    public decimal? TurbidityCm { get; set; }
    public string? Notes { get; set; }
}
