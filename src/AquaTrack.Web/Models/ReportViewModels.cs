using AquaTrack.Application.Reporting;

namespace AquaTrack.Web.Models;

public record ReportEntry(ReportKind Kind, string Title, string Description);

public record ReportGroup(string Name, string Icon, IReadOnlyList<ReportEntry> Reports);

public class ReportCatalogueViewModel
{
    public IReadOnlyList<ReportGroup> Groups { get; init; } = [];
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

public class ReportViewModel
{
    public ReportKind Kind { get; init; }
    public required ReportTable Table { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Route values carried onto the export links so they match what is on screen.</summary>
    public object RouteValues(ReportFormat? format = null) => format is null
        ? new
        {
            kind = Kind,
            from = From.ToString("yyyy-MM-dd"),
            to = To.ToString("yyyy-MM-dd"),
        }
        : new
        {
            kind = Kind,
            format,
            from = From.ToString("yyyy-MM-dd"),
            to = To.ToString("yyyy-MM-dd"),
        };

    public static string AlignClass(ReportAlign align) => align switch
    {
        ReportAlign.Right => "text-end",
        ReportAlign.Center => "text-center",
        _ => "text-start",
    };
}
