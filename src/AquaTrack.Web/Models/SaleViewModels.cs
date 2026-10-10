using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public class SaleListViewModel
{
    public required PagedList<Sale> Page { get; init; }
    public required ListQuery Query { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool OnlyWithDues { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (From is { } f) values["from"] = f.ToString("yyyy-MM-dd");
        if (To is { } t) values["to"] = t.ToString("yyyy-MM-dd");
        if (OnlyWithDues) values["dues"] = "true";
        return values;
    }

    public decimal PageTotal => Page.Items.Sum(s => s.GrandTotal);
    public decimal PagePaid => Page.Items.Sum(s => s.PaidAmount);
    public decimal PageDue => Page.Items.Sum(s => s.DueAmount);
    public decimal PageQuantityKg => Page.Items.Sum(s => s.Items.Sum(i => i.QuantityKg));
}

/// <summary>One line of a sale. Blank lines are dropped before posting.</summary>
public class SaleLineViewModel
{
    [Display(Name = "Species")]
    public int FishTypeId { get; set; }

    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Display(Name = "Batch")]
    public int FishStockId { get; set; }

    [Range(0.001, 1_000_000, ErrorMessage = "Enter the quantity in kg.")]
    [Display(Name = "Quantity (kg)")]
    public decimal? QuantityKg { get; set; }

    [Range(1, 10_000_000)]
    [Display(Name = "Pieces")]
    public int? PieceCount { get; set; }

    [Range(0.01, 1_000_000, ErrorMessage = "Enter the price per kg.")]
    [Display(Name = "Price per kg")]
    public decimal? UnitPrice { get; set; }

    public bool IsFilled => FishTypeId > 0 && QuantityKg is > 0 && UnitPrice is > 0;

    public decimal LineTotal => Math.Round((QuantityKg ?? 0) * (UnitPrice ?? 0), 2);
}

public class SaleFormViewModel
{
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Display(Name = "Sale date")]
    public DateOnly SaleDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public List<SaleLineViewModel> Items { get; set; } = [];

    [Range(0, 10_000_000, ErrorMessage = "Enter a discount of zero or more.")]
    [Display(Name = "Discount")]
    public decimal Discount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(0, 100_000_000, ErrorMessage = "Enter the amount received.")]
    [Display(Name = "Paid now")]
    public decimal PaidAmount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Customers { get; set; } = [];
    public IReadOnlyList<SelectOption> Species { get; set; } = [];
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];

    public decimal SubTotal => Items.Where(i => i.IsFilled).Sum(i => i.LineTotal);
    public decimal GrandTotal => SubTotal - Discount;
    public decimal DueAmount => GrandTotal - PaidAmount;
}

public class SalePaymentViewModel
{
    public int SaleId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal DueAmount { get; set; }

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount received.")]
    [Display(Name = "Amount received")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
}
