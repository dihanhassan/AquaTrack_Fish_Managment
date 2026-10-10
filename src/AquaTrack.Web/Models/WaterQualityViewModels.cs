using System.ComponentModel.DataAnnotations;
using AquaTrack.Domain.Entities;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

/// <summary>
/// The ranges a pond should normally sit within. Readings outside these are highlighted, because a
/// drifting value usually explains a rise in mortality before anything else does.
/// </summary>
public static class WaterQualityRanges
{
    public static (decimal Min, decimal Max) Ph => (6.5m, 8.5m);
    public static (decimal Min, decimal Max) TemperatureC => (22m, 32m);
    public static (decimal Min, decimal Max) DissolvedOxygen => (5m, 12m);
    public static (decimal Min, decimal Max) Ammonia => (0m, 0.5m);
    public static (decimal Min, decimal Max) Nitrite => (0m, 0.5m);

    public static bool IsOutOfRange(decimal? value, (decimal Min, decimal Max) range)
        => value is { } v && (v < range.Min || v > range.Max);

    /// <summary>CSS class for a reading cell: red when outside the safe band.</summary>
    public static string ToneFor(decimal? value, (decimal Min, decimal Max) range)
        => IsOutOfRange(value, range) ? "text-danger fw-semibold" : string.Empty;
}

public class WaterQualityListViewModel
{
    public required PagedList<WaterQualityLog> Page { get; init; }
    public required ListQuery Query { get; init; }
    public int? PondId { get; init; }
    public IReadOnlyList<SelectOption> Ponds { get; init; } = [];

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (PondId is { } pond) values["pondId"] = pond.ToString();
        return values;
    }

    /// <summary>How many readings on this page have at least one value outside its safe band.</summary>
    public int FlaggedCount => Page.Items.Count(log =>
        WaterQualityRanges.IsOutOfRange(log.Ph, WaterQualityRanges.Ph) ||
        WaterQualityRanges.IsOutOfRange(log.TemperatureC, WaterQualityRanges.TemperatureC) ||
        WaterQualityRanges.IsOutOfRange(log.DissolvedOxygenMgL, WaterQualityRanges.DissolvedOxygen) ||
        WaterQualityRanges.IsOutOfRange(log.AmmoniaMgL, WaterQualityRanges.Ammonia) ||
        WaterQualityRanges.IsOutOfRange(log.NitriteMgL, WaterQualityRanges.Nitrite));
}

public class WaterQualityFormViewModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a pond.")]
    [Display(Name = "Pond")]
    public int PondId { get; set; }

    [Display(Name = "Date")]
    public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 14, ErrorMessage = "pH runs from 0 to 14.")]
    [Display(Name = "pH")]
    public decimal? Ph { get; set; }

    [Range(0, 60, ErrorMessage = "Enter a temperature between 0 and 60 degrees.")]
    [Display(Name = "Temperature (C)")]
    public decimal? TemperatureC { get; set; }

    [Range(0, 30, ErrorMessage = "Enter dissolved oxygen between 0 and 30 mg/L.")]
    [Display(Name = "Dissolved oxygen (mg/L)")]
    public decimal? DissolvedOxygenMgL { get; set; }

    [Range(0, 100, ErrorMessage = "Enter ammonia between 0 and 100 mg/L.")]
    [Display(Name = "Ammonia (mg/L)")]
    public decimal? AmmoniaMgL { get; set; }

    [Range(0, 100, ErrorMessage = "Enter nitrite between 0 and 100 mg/L.")]
    [Display(Name = "Nitrite (mg/L)")]
    public decimal? NitriteMgL { get; set; }

    [Range(0, 500, ErrorMessage = "Enter turbidity between 0 and 500 cm.")]
    [Display(Name = "Turbidity (cm)")]
    public decimal? TurbidityCm { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectOption> Ponds { get; set; } = [];

    public bool IsEdit => Id.HasValue;

    /// <summary>A reading with every field blank records nothing, so it is rejected.</summary>
    public bool HasAnyReading =>
        Ph is not null || TemperatureC is not null || DissolvedOxygenMgL is not null
        || AmmoniaMgL is not null || NitriteMgL is not null || TurbidityCm is not null;
}
