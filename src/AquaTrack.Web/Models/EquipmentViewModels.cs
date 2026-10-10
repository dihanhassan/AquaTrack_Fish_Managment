using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class EquipmentListViewModel
{
    public required PagedList<Equipment> Page { get; init; }
    public required ListQuery Query { get; init; }
    public string? ConditionFilter { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (!string.IsNullOrEmpty(ConditionFilter)) values["condition"] = ConditionFilter;
        return values;
    }

    public decimal PageValue => Page.Items.Sum(e => e.TotalPurchaseCost);

    public static string ConditionTone(EquipmentCondition condition) => condition switch
    {
        EquipmentCondition.New or EquipmentCondition.Good => "success",
        EquipmentCondition.Fair => "info",
        EquipmentCondition.NeedsRepair => "warning",
        _ => "danger",
    };
}

public class EquipmentFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the equipment name.")]
    [StringLength(150)]
    [Display(Name = "Equipment name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Code")]
    public string? Code { get; set; }

    [Display(Name = "Category")]
    public EquipmentCategory Category { get; set; } = EquipmentCategory.Other;

    [StringLength(120)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }

    [StringLength(80)]
    [Display(Name = "Model no.")]
    public string? ModelNo { get; set; }

    [StringLength(80)]
    [Display(Name = "Serial no.")]
    public string? SerialNo { get; set; }

    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [Display(Name = "Assigned to pond")]
    public int? PondId { get; set; }

    [Display(Name = "Purchase date")]
    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 100_000_000, ErrorMessage = "Enter the purchase price.")]
    [Display(Name = "Purchase price")]
    public decimal PurchasePrice { get; set; }

    [Range(1, 10000, ErrorMessage = "Enter how many were bought.")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; } = 1;

    [Display(Name = "Condition")]
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.New;

    [Display(Name = "Warranty until")]
    public DateOnly? WarrantyUntil { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];

    public bool IsEdit => Id.HasValue;
}

public class EquipmentDetailsViewModel
{
    public required Equipment Equipment { get; init; }
    public IReadOnlyList<EquipmentPart> Parts { get; init; } = [];
    public IReadOnlyList<EquipmentMaintenance> Maintenance { get; init; } = [];

    public decimal PartsValue => Parts.Sum(p => p.StockValue);
    public decimal MaintenanceCost => Maintenance.Sum(m => m.Cost);

    /// <summary>What the machine has cost in total: purchase plus every repair since.</summary>
    public decimal TotalCostOfOwnership => Equipment.TotalPurchaseCost + MaintenanceCost;

    public EquipmentMaintenance? NextDue => Maintenance
        .Where(m => m.NextDueDate is not null)
        .OrderByDescending(m => m.MaintenanceDate)
        .FirstOrDefault();

    public IEnumerable<EquipmentPart> LowStockParts => Parts.Where(p => p.IsLowStock);
}

public class EquipmentPartFormViewModel
{
    public int? Id { get; set; }
    public int EquipmentId { get; set; }

    [Required(ErrorMessage = "Enter the part name.")]
    [StringLength(150)]
    [Display(Name = "Part name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Part no.")]
    public string? PartNo { get; set; }

    [Display(Name = "Unit")]
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Piece;

    [Range(0, 1_000_000, ErrorMessage = "Enter the quantity held.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a reorder level of zero or more.")]
    [Display(Name = "Reorder level")]
    public decimal ReorderLevel { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Enter the unit price.")]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Purchase date")]
    public DateOnly? PurchaseDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class MaintenanceFormViewModel
{
    public int EquipmentId { get; set; }

    [Display(Name = "Date")]
    public DateOnly MaintenanceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Type")]
    public MaintenanceType MaintenanceType { get; set; } = MaintenanceType.Routine;

    [StringLength(500)]
    [Display(Name = "What was done")]
    public string? Description { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Enter the cost, or zero.")]
    [Display(Name = "Cost")]
    public decimal Cost { get; set; }

    [StringLength(150)]
    [Display(Name = "Performed by")]
    public string? PerformedBy { get; set; }

    [Display(Name = "Next due")]
    public DateOnly? NextDueDate { get; set; }

    [Display(Name = "Condition afterwards")]
    public EquipmentCondition? ConditionAfter { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
