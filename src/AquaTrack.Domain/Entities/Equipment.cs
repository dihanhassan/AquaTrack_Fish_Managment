using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class Equipment : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public EquipmentCategory Category { get; set; } = EquipmentCategory.Other;
    public string? Brand { get; set; }
    public string? ModelNo { get; set; }
    public string? SerialNo { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public int Quantity { get; set; } = 1;
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.New;
    public DateOnly? WarrantyUntil { get; set; }

    public int? PondId { get; set; }
    public Pond? Pond { get; set; }
    public string? Notes { get; set; }

    public ICollection<EquipmentPart> Parts { get; set; } = [];
    public ICollection<EquipmentMaintenance> MaintenanceRecords { get; set; } = [];

    public decimal TotalPurchaseCost => PurchasePrice * Quantity;
    public bool IsUnderWarranty => WarrantyUntil.HasValue && WarrantyUntil.Value >= DateOnly.FromDateTime(DateTime.Today);
}
