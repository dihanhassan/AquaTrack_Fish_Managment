using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class PurchaseListViewModel
{
    public required PagedList<Purchase> Page { get; init; }
    public required ListQuery Query { get; init; }
    public bool OnlyWithDues { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (OnlyWithDues) values["dues"] = "true";
        return values;
    }

    public decimal PageTotal => Page.Items.Sum(p => p.GrandTotal);
    public decimal PagePaid => Page.Items.Sum(p => p.PaidAmount);
    public decimal PageDue => Page.Items.Sum(p => p.DueAmount);
}

/// <summary>
/// One purchase line. <see cref="ItemType"/> decides what <see cref="TargetId"/> points at, which
/// is what tells the posting service whether to raise feed stock, medicine stock or nothing.
/// </summary>
public class PurchaseLineViewModel
{
    [Display(Name = "Type")]
    public PurchaseItemType ItemType { get; set; } = PurchaseItemType.Feed;

    /// <summary>Id of the feed item, medicine, equipment or species, depending on the type.</summary>
    [Display(Name = "Item")]
    public int TargetId { get; set; }

    [StringLength(250)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Unit")]
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Kg;

    [Range(0.001, 10_000_000, ErrorMessage = "Enter the quantity.")]
    [Display(Name = "Quantity")]
    public decimal? Quantity { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Enter the unit price.")]
    [Display(Name = "Unit price")]
    public decimal? UnitPrice { get; set; }

    public bool IsFilled => !string.IsNullOrWhiteSpace(Description) && Quantity is > 0 && UnitPrice is > 0;

    public decimal LineTotal => Math.Round((Quantity ?? 0) * (UnitPrice ?? 0), 2);
}

public class PurchaseFormViewModel
{
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [Display(Name = "Purchase date")]
    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public List<PurchaseLineViewModel> Items { get; set; } = [];

    [Range(0, 10_000_000, ErrorMessage = "Enter a discount of zero or more.")]
    [Display(Name = "Discount")]
    public decimal Discount { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Enter a transport cost of zero or more.")]
    [Display(Name = "Transport")]
    public decimal TransportCost { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(0, 100_000_000, ErrorMessage = "Enter the amount paid.")]
    [Display(Name = "Paid now")]
    public decimal PaidAmount { get; set; }

    [StringLength(60)]
    [Display(Name = "Invoice no.")]
    public string? InvoiceNo { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Suppliers { get; set; } = [];

    public decimal SubTotal => Items.Where(i => i.IsFilled).Sum(i => i.LineTotal);
    public decimal GrandTotal => SubTotal - Discount + TransportCost;
    public decimal DueAmount => GrandTotal - PaidAmount;
}

public class PurchasePaymentViewModel
{
    public int PurchaseId { get; set; }
    public string PurchaseNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal DueAmount { get; set; }

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount paid.")]
    [Display(Name = "Amount paid")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
}
