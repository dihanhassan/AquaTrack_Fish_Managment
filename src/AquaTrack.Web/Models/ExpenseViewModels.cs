using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

public record CategoryTotal(string Name, decimal Amount);

public class ExpenseListViewModel
{
    public required PagedList<Expense> Page { get; init; }
    public required ListQuery Query { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int? CategoryId { get; init; }

    public IReadOnlyList<SelectOption> Categories { get; init; } = [];

    /// <summary>Total for the whole filtered period, not just the page being shown.</summary>
    public decimal PeriodTotal { get; init; }
    public IReadOnlyList<CategoryTotal> ByCategory { get; init; } = [];

    public CategoryTotal? Largest => ByCategory.Count > 0 ? ByCategory[0] : null;

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        values["from"] = From.ToString("yyyy-MM-dd");
        values["to"] = To.ToString("yyyy-MM-dd");
        if (CategoryId is { } id) values["categoryId"] = id.ToString();
        return values;
    }

    public decimal ShareOf(CategoryTotal category)
        => PeriodTotal <= 0 ? 0 : Math.Round(category.Amount / PeriodTotal * 100, 1);
}

public class ExpenseFormViewModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int ExpenseCategoryId { get; set; }

    [Display(Name = "Date")]
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount spent.")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(40)]
    [Display(Name = "Voucher no.")]
    public string? VoucherNo { get; set; }

    [Display(Name = "Pond")]
    public int? PondId { get; set; }

    public IReadOnlyList<SelectOption> Categories { get; set; } = [];
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];

    public bool IsEdit => Id.HasValue;
}

public class OtherIncomeFormViewModel
{
    [Display(Name = "Date")]
    public DateOnly IncomeDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Say where the income came from.")]
    [StringLength(200)]
    [Display(Name = "Source")]
    public string Source { get; set; } = string.Empty;

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount received.")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

public class ExpenseCategoryFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter a category name.")]
    [StringLength(120)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Built-in categories can be renamed or deactivated, but not deleted.</summary>
    public bool IsSystem { get; set; }

    public bool IsEdit => Id.HasValue;
}
