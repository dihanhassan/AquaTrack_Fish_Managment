using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>Key/value configuration editable from the Settings screen (farm name, currency, opening cash…).</summary>
public class FarmSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Group { get; set; } = "General";
}
