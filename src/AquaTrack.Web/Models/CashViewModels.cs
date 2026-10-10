using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Services;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Web.Models;

public record CashCategoryTotal(string Category, CashDirection Direction, decimal Amount);

public class CashBookViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    public required CashSummary Summary { get; init; }
    public IReadOnlyList<CashLedgerRow> Rows { get; init; } = [];
    public IReadOnlyList<CashCategoryTotal> ByCategory { get; init; } = [];

    public IEnumerable<CashCategoryTotal> Inflows => ByCategory.Where(c => c.Direction == CashDirection.In);
    public IEnumerable<CashCategoryTotal> Outflows => ByCategory.Where(c => c.Direction == CashDirection.Out);

    /// <summary>
    /// The arithmetic the screen is claiming. Shown so a reader can check the ledger adds up
    /// rather than taking the closing figure on trust.
    /// </summary>
    public string BalanceEquation =>
        $"{Summary.OpeningBalance:N2} + {Summary.TotalIn:N2} − {Summary.TotalOut:N2} = {Summary.ClosingBalance:N2}";
}

public class CashEntryFormViewModel
{
    [Display(Name = "Date")]
    public DateOnly TransactionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Direction")]
    public CashDirection Direction { get; set; } = CashDirection.Out;

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount.")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Enter a category.")]
    [StringLength(120)]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}
