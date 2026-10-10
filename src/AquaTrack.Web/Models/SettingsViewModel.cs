using System.ComponentModel.DataAnnotations;

namespace AquaTrack.Web.Models;

public class SettingsViewModel
{
    [Required(ErrorMessage = "Enter the farm name.")]
    [StringLength(150)]
    [Display(Name = "Farm name")]
    public string FarmName { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Owner")]
    public string? FarmOwner { get; set; }

    [StringLength(400)]
    [Display(Name = "Address")]
    public string? FarmAddress { get; set; }

    [StringLength(40)]
    [Display(Name = "Phone")]
    public string? FarmPhone { get; set; }

    [Required(ErrorMessage = "Enter a currency code.")]
    [StringLength(10)]
    [Display(Name = "Currency code")]
    public string CurrencyCode { get; set; } = "BDT";

    [Required(ErrorMessage = "Enter a currency symbol.")]
    [StringLength(10)]
    [Display(Name = "Currency symbol")]
    public string CurrencySymbol { get; set; } = "Tk";

    [Display(Name = "Opening cash balance")]
    public decimal OpeningCashBalance { get; set; }

    [Range(1, 90, ErrorMessage = "Enter between 1 and 90 days.")]
    [Display(Name = "Low stock warning (days)")]
    public int LowStockWarningDays { get; set; } = 7;

    [Range(1, 365, ErrorMessage = "Enter between 1 and 365 days.")]
    [Display(Name = "Medicine expiry warning (days)")]
    public int MedicineExpiryWarningDays { get; set; } = 30;

    [Range(0.1, 100, ErrorMessage = "Enter a percentage between 0.1 and 100.")]
    [Display(Name = "High mortality threshold (%)")]
    public decimal HighMortalityThresholdPercent { get; set; } = 2m;

    [Range(1, 5, ErrorMessage = "Enter a sensitivity between 1 and 5.")]
    [Display(Name = "Cost spike sensitivity")]
    public decimal CostSpikeSigma { get; set; } = 2m;
}
