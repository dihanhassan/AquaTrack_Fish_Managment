using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class MedicineListViewModel
{
    public required PagedList<Medicine> Page { get; init; }
    public required ListQuery Query { get; init; }

    /// <summary>One of "low", "expiring", "expired", or empty for everything.</summary>
    public string? Filter { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (!string.IsNullOrEmpty(Filter)) values["filter"] = Filter;
        return values;
    }

    public decimal TotalStockValue => Page.Items.Sum(m => m.StockValue);
}

public class MedicineFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the medicine name.")]
    [StringLength(150)]
    [Display(Name = "Medicine name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }

    [Display(Name = "Type")]
    public MedicineType MedicineType { get; set; } = MedicineType.Other;

    [Display(Name = "Unit")]
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;

    [Display(Name = "Default supplier")]
    public int? SupplierId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a reorder level of zero or more.")]
    [Display(Name = "Reorder level")]
    public decimal ReorderLevel { get; set; }

    [Display(Name = "Expiry date")]
    public DateOnly? ExpiryDate { get; set; }

    [StringLength(250)]
    [Display(Name = "Dosage")]
    public string? Dosage { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];

    public bool IsEdit => Id.HasValue;
}

public class MedicinePurchaseFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a medicine.")]
    [Display(Name = "Medicine")]
    public int MedicineId { get; set; }

    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [Display(Name = "Purchase date")]
    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.001, 10_000_000, ErrorMessage = "Enter the quantity purchased.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Enter the price per unit.")]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(0, 100_000_000, ErrorMessage = "Enter the amount paid.")]
    [Display(Name = "Paid amount")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "Expiry date")]
    public DateOnly? ExpiryDate { get; set; }

    [StringLength(60)]
    [Display(Name = "Batch no.")]
    public string? BatchNo { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Medicines { get; set; } = [];
    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];

    public decimal TotalPrice => Math.Round(Quantity * UnitPrice, 2);
    public decimal DueAmount => TotalPrice - PaidAmount;
}

public class MedicineUsageFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a medicine.")]
    [Display(Name = "Medicine")]
    public int MedicineId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a pond.")]
    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Display(Name = "Date")]
    public DateOnly UsageDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.001, 1_000_000, ErrorMessage = "Enter the quantity used.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    [StringLength(250)]
    [Display(Name = "Purpose")]
    public string? Purpose { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Medicines { get; set; } = [];
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];
}

public class MedicineDetailsViewModel
{
    public required Medicine Medicine { get; init; }
    public IReadOnlyList<MedicinePurchase> Purchases { get; init; } = [];
    public IReadOnlyList<MedicineUsage> Usages { get; init; } = [];

    public decimal TotalPurchased => Purchases.Sum(p => p.Quantity);
    public decimal TotalPurchaseCost => Purchases.Sum(p => p.TotalPrice);
    public decimal TotalUsed => Usages.Sum(u => u.Quantity);
    public decimal TotalUsageCost => Usages.Sum(u => u.TotalCost);
}
