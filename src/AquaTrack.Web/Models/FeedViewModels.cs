using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class FeedListViewModel
{
    public required PagedList<FeedItem> Page { get; init; }
    public required ListQuery Query { get; init; }
    public bool OnlyLowStock { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (OnlyLowStock) values["low"] = "true";
        return values;
    }

    public decimal TotalStockValue => Page.Items.Sum(f => f.StockValue);
    public int LowStockCount => Page.Items.Count(f => f.IsLowStock);
}

public class FeedItemFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the feed name.")]
    [StringLength(150)]
    [Display(Name = "Feed name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }

    [Display(Name = "Feed type")]
    public FeedType FeedType { get; set; } = FeedType.Floating;

    [Display(Name = "Unit")]
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;

    [Display(Name = "Default supplier")]
    public int? SupplierId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a reorder level of zero or more.")]
    [Display(Name = "Reorder level")]
    public decimal ReorderLevel { get; set; }

    [Range(0, 100, ErrorMessage = "Protein content must be between 0 and 100 percent.")]
    [Display(Name = "Protein (%)")]
    public decimal? ProteinPercent { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];

    public bool IsEdit => Id.HasValue;
}

public class FeedPurchaseFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a feed item.")]
    [Display(Name = "Feed item")]
    public int FeedItemId { get; set; }

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

    [StringLength(60)]
    [Display(Name = "Invoice no.")]
    public string? InvoiceNo { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> FeedItems { get; set; } = [];
    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];

    /// <summary>Computed rather than posted, so the stored total always matches the line figures.</summary>
    public decimal TotalPrice => Math.Round(Quantity * UnitPrice, 2);
    public decimal DueAmount => TotalPrice - PaidAmount;
}

public class FeedUsageFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a feed item.")]
    [Display(Name = "Feed item")]
    public int FeedItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a pond.")]
    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Display(Name = "Date")]
    public DateOnly UsageDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.001, 1_000_000, ErrorMessage = "Enter the quantity fed.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> FeedItems { get; set; } = [];
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];
}

public class FeedDetailsViewModel
{
    public required FeedItem Item { get; init; }
    public IReadOnlyList<FeedPurchase> Purchases { get; init; } = [];
    public IReadOnlyList<FeedUsage> Usages { get; init; } = [];

    public decimal TotalPurchased => Purchases.Sum(p => p.Quantity);
    public decimal TotalPurchaseCost => Purchases.Sum(p => p.TotalPrice);
    public decimal TotalUsed => Usages.Sum(u => u.Quantity);
    public decimal TotalUsageCost => Usages.Sum(u => u.TotalCost);
}
