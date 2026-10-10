using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Services;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Web.Models;

/// <summary>What is already on record for this pond and day, shown above the form.</summary>
public record DailyLogSummary(
    decimal FeedKg,
    decimal FeedCost,
    decimal MedicineCost,
    int Mortality,
    decimal Sales,
    decimal OtherIncome,
    decimal Expense,
    bool IsLocked)
{
    public bool HasActivity => FeedKg > 0 || MedicineCost > 0 || Mortality > 0
                               || Sales > 0 || OtherIncome > 0 || Expense > 0;
}

public class DailyFeedLineViewModel
{
    public int FeedItemId { get; set; }

    /// <summary>Nullable so an untouched row binds as empty rather than failing the whole form.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of zero or more.")]
    public decimal? Quantity { get; set; }

    public bool IsFilled => FeedItemId > 0 && Quantity is > 0;
}

public class DailyMedicineLineViewModel
{
    public int MedicineId { get; set; }

    /// <summary>Nullable for the same reason as the feed line above.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of zero or more.")]
    public decimal? Quantity { get; set; }

    [StringLength(250)]
    public string? Purpose { get; set; }

    public bool IsFilled => MedicineId > 0 && Quantity is > 0;
}

/// <summary>
/// The whole daily screen as one model. Every section is optional; the user fills in what
/// happened and leaves the rest blank.
/// </summary>
public class DailyInputViewModel
{
    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Display(Name = "Date")]
    public DateOnly LogDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(120)]
    [Display(Name = "Weather")]
    public string? Weather { get; set; }

    [StringLength(2000)]
    [Display(Name = "Observations")]
    public string? Observations { get; set; }

    [Display(Name = "Batch")]
    public int? FishStockId { get; set; }

    public List<DailyFeedLineViewModel> FeedLines { get; set; } = [];
    public List<DailyMedicineLineViewModel> MedicineLines { get; set; } = [];

    // Mortality
    [Range(0, 10_000_000, ErrorMessage = "Enter how many fish died.")]
    [Display(Name = "Fish died")]
    public int? MortalityCount { get; set; }

    [Display(Name = "Likely cause")]
    public MortalityCause MortalityCause { get; set; } = MortalityCause.Unknown;

    // Growth sample
    [Range(0, 100000, ErrorMessage = "Enter the sampled average weight.")]
    [Display(Name = "Average weight (g)")]
    public decimal? SampleAvgWeightGram { get; set; }

    [Range(1, 1000)]
    [Display(Name = "Sample size")]
    public int? SampleSize { get; set; }

    // Sale
    [Range(0, 1_000_000, ErrorMessage = "Enter the quantity sold.")]
    [Display(Name = "Quantity sold (kg)")]
    public decimal? SaleQuantityKg { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter the price per kg.")]
    [Display(Name = "Price per kg")]
    public decimal? SaleUnitPrice { get; set; }

    [Display(Name = "Species sold")]
    public int? SaleFishTypeId { get; set; }

    [Display(Name = "Customer")]
    public int? SaleCustomerId { get; set; }

    [Display(Name = "Payment")]
    public PaymentMethod SalePaymentMethod { get; set; } = PaymentMethod.Cash;

    // Other income
    [Range(0, 10_000_000, ErrorMessage = "Enter the amount received.")]
    [Display(Name = "Other income")]
    public decimal? OtherIncomeAmount { get; set; }

    [StringLength(200)]
    [Display(Name = "Source")]
    public string? OtherIncomeSource { get; set; }

    // Expense
    [Range(0, 10_000_000, ErrorMessage = "Enter the amount spent.")]
    [Display(Name = "Expense")]
    public decimal? ExpenseAmount { get; set; }

    [Display(Name = "Category")]
    public int? ExpenseCategoryId { get; set; }

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? ExpenseDescription { get; set; }

    // Context, filled by the controller
    public string PondName { get; set; } = "-";
    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];
    public IReadOnlyList<SelectOption> Batches { get; set; } = [];
    public IReadOnlyList<SelectOption> FeedItems { get; set; } = [];
    public IReadOnlyList<SelectOption> Medicines { get; set; } = [];
    public IReadOnlyList<SelectOption> Species { get; set; } = [];
    public IReadOnlyList<SelectOption> Customers { get; set; } = [];
    public IReadOnlyList<SelectOption> ExpenseCategories { get; set; } = [];
    public DailyLogSummary? AlreadyRecorded { get; set; }
    public string? FeedAdvice { get; set; }

    public bool IsLocked => AlreadyRecorded?.IsLocked == true;

    /// <summary>True when at least one section was filled in, so an empty save can be rejected.</summary>
    public bool HasAnything =>
        FeedLines.Any(l => l.IsFilled)
        || MedicineLines.Any(l => l.IsFilled)
        || MortalityCount is > 0
        || SampleAvgWeightGram is > 0
        || (SaleQuantityKg is > 0 && SaleUnitPrice is > 0)
        || OtherIncomeAmount is > 0
        || ExpenseAmount is > 0
        || !string.IsNullOrWhiteSpace(Observations)
        || !string.IsNullOrWhiteSpace(Weather);

    /// <summary>Maps to the application-layer command, dropping the rows left blank.</summary>
    public DailyInput ToInput() => new()
    {
        LogDate = LogDate,
        PondId = PondId,
        Weather = Weather,
        Observations = Observations,
        FishStockId = FishStockId,

        FeedLines = [.. FeedLines
            .Where(l => l.IsFilled)
            .Select(l => new DailyFeedLine { FeedItemId = l.FeedItemId, Quantity = l.Quantity!.Value })],

        MedicineLines = [.. MedicineLines
            .Where(l => l.IsFilled)
            .Select(l => new DailyMedicineLine
            {
                MedicineId = l.MedicineId,
                Quantity = l.Quantity!.Value,
                Purpose = l.Purpose,
            })],

        MortalityCount = MortalityCount,
        MortalityCause = MortalityCause,
        SampleAvgWeightGram = SampleAvgWeightGram,
        SampleSize = SampleSize,

        SaleQuantityKg = SaleQuantityKg,
        SaleUnitPrice = SaleUnitPrice,
        SaleFishTypeId = SaleFishTypeId,
        SaleCustomerId = SaleCustomerId,
        SalePaymentMethod = SalePaymentMethod,

        OtherIncomeAmount = OtherIncomeAmount,
        OtherIncomeSource = OtherIncomeSource,

        ExpenseAmount = ExpenseAmount,
        ExpenseCategoryId = ExpenseCategoryId,
        ExpenseDescription = ExpenseDescription,
    };
}
