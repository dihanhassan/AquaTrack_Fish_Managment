using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class EquipmentMaintenance : BaseEntity
{
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public DateOnly MaintenanceDate { get; set; }
    public MaintenanceType MaintenanceType { get; set; } = MaintenanceType.Routine;
    public string? Description { get; set; }
    public decimal Cost { get; set; }
    public string? PerformedBy { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public EquipmentCondition? ConditionAfter { get; set; }
    public string? Notes { get; set; }
}
