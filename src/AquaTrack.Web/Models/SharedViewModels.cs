using AquaTrack.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Models;

/// <summary>Drives the shared empty-state partial shown when a list has no rows.</summary>
public class EmptyStateViewModel
{
    public string Icon { get; init; } = "bi-inbox";
    public string Title { get; init; } = "Nothing here yet";
    public string Message { get; init; } = "Records you add will appear in this list.";
    public string? ActionLabel { get; init; }
    public string? ActionUrl { get; init; }

    /// <summary>The variant shown when a search or filter excluded everything.</summary>
    public static EmptyStateViewModel NoMatches(string? search = null) => new()
    {
        Icon = "bi-search",
        Title = "No matching records",
        Message = string.IsNullOrWhiteSpace(search)
            ? "Try widening the filters above."
            : $"Nothing matched \"{search}\". Try a different search.",
    };
}

/// <summary>
/// Drives the shared pager. It rebuilds the list URL per page so every page link carries the
/// current search, sort and page size with it.
/// </summary>
public class PagerViewModel
{
    public required string Url { get; init; }
    public required string Target { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages { get; init; } = 1;
    public int FirstItemIndex { get; init; }
    public int LastItemIndex { get; init; }

    /// <summary>Extra query values (search, sort, filters) carried onto every page link.</summary>
    public IDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>();

    public string PageUrl(int page)
    {
        var values = RouteValues
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "page")
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToList();

        values.Add($"page={Math.Clamp(page, 1, Math.Max(1, TotalPages))}");

        return $"{Url}?{string.Join("&", values)}";
    }

    public static PagerViewModel From<T>(
        PagedList<T> page, string url, string target, IDictionary<string, string?>? routeValues = null) => new()
    {
        Url = url,
        Target = target,
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
        TotalPages = page.TotalPages,
        FirstItemIndex = page.FirstItemIndex,
        LastItemIndex = page.LastItemIndex,
        RouteValues = routeValues ?? new Dictionary<string, string?>(),
    };
}

/// <summary>A sortable column header; clicking it toggles the direction.</summary>
public class SortHeaderViewModel
{
    public required string Label { get; init; }
    public required string Key { get; init; }
    public required string Url { get; init; }
    public required string Target { get; init; }
    public string? CurrentSort { get; init; }
    public bool CurrentDesc { get; init; }
    public string Align { get; init; } = "start";
    public IDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>();

    public bool IsActive => string.Equals(CurrentSort, Key, StringComparison.OrdinalIgnoreCase);

    /// <summary>Clicking the active column flips the direction; a new column starts ascending.</summary>
    public bool NextDesc => IsActive && !CurrentDesc;

    public string Icon => !IsActive ? "bi-arrow-down-up" : CurrentDesc ? "bi-sort-down" : "bi-sort-up";

    public string SortUrl()
    {
        var values = RouteValues
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key is not ("sort" or "desc" or "page"))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToList();

        values.Add($"sort={Uri.EscapeDataString(Key)}");
        if (NextDesc) values.Add("desc=true");

        return $"{Url}?{string.Join("&", values)}";
    }
}

/// <summary>A confirm-and-delete dialog rendered into the shared modal.</summary>
public class ConfirmViewModel
{
    public string Title { get; init; } = "Delete record";
    public required string Message { get; init; }
    public string? Detail { get; init; }
    public required string Action { get; init; }
    public string Method { get; init; } = "delete";
    public string ConfirmLabel { get; init; } = "Delete";
    public string ConfirmClass { get; init; } = "btn-danger";
}

/// <summary>A dashboard KPI tile.</summary>
public class StatCardViewModel
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string Icon { get; init; } = "bi-graph-up";
    public string Tone { get; init; } = "primary";
    public string? Meta { get; init; }
    public string? Url { get; init; }
}
