using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Infrastructure;

/// <summary>One page of rows plus the counts the pager partial needs.</summary>
public class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public int FirstItemIndex => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItemIndex => Math.Min(Page * PageSize, TotalCount);

    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> source, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 200);

        var total = await source.CountAsync(ct);

        // Deleting the last row of a page would otherwise leave the user on an empty page.
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > lastPage) page = lastPage;

        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedList<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public static PagedList<T> Empty(int pageSize = 20) => new() { PageSize = pageSize };
}

/// <summary>Query-string state shared by every list screen: search, sort, and paging.</summary>
public class ListQuery
{
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Builds the query string an HTMX control should send to keep the other filters intact.</summary>
    public IDictionary<string, string?> ToRouteValues() => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["sort"] = Sort,
        ["desc"] = Desc ? "true" : null,
        ["page"] = Page > 1 ? Page.ToString() : null,
        ["pageSize"] = PageSize != 20 ? PageSize.ToString() : null,
    };
}

public static class QueryableExtensions
{
    /// <summary>
    /// Applies a sort chosen by name from a whitelist, so the column a user clicks cannot become
    /// an arbitrary expression.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> source,
        string? sort,
        bool descending,
        IReadOnlyDictionary<string, System.Linq.Expressions.Expression<Func<T, object>>> allowed,
        string defaultKey)
    {
        var key = !string.IsNullOrWhiteSpace(sort) && allowed.ContainsKey(sort) ? sort : defaultKey;
        var selector = allowed[key];

        return descending ? source.OrderByDescending(selector) : source.OrderBy(selector);
    }
}
