namespace AquaTrack.Application.Reporting;

/// <summary>
/// A report is reduced to a title, a date range and a grid of strings before it is exported, so a
/// single PDF writer and a single Excel writer can render every report in the system.
/// </summary>
public class ReportTable
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public IReadOnlyList<ReportColumn> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];

    /// <summary>Label/value pairs rendered above the grid, such as totals or an opening balance.</summary>
    public IReadOnlyList<(string Label, string Value)> Summary { get; init; } = [];

    /// <summary>Optional totals row rendered at the bottom; must match the column count when present.</summary>
    public IReadOnlyList<string>? TotalsRow { get; init; }

    public string PeriodLabel => (From, To) switch
    {
        ({ } f, { } t) => $"{f:dd MMM yyyy} to {t:dd MMM yyyy}",
        ({ } f, null) => $"From {f:dd MMM yyyy}",
        (null, { } t) => $"Up to {t:dd MMM yyyy}",
        _ => "All time",
    };
}

public record ReportColumn(string Header, ReportAlign Align = ReportAlign.Left, float Width = 1f);

public enum ReportAlign { Left, Right, Center }
