using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

public class Medicine : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public MedicineType MedicineType { get; set; } = MedicineType.Other;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal LastUnitPrice { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Dosage { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MedicinePurchase> Purchases { get; set; } = [];
    public ICollection<MedicineUsage> Usages { get; set; } = [];

    public decimal StockValue => CurrentStock * AverageUnitCost;
    public bool IsLowStock => ReorderLevel > 0 && CurrentStock <= ReorderLevel;
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateOnly.FromDateTime(DateTime.Today);
    public bool IsExpiringSoon => ExpiryDate.HasValue && !IsExpired
        && ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.Today.AddDays(30));
}
