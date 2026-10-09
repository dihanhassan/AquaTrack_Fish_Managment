using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AquaTrack.Web.Infrastructure;

/// <summary>
/// The HTMX request/response contract used across the app. Controllers return full pages to a normal
/// browser navigation and partials to an HTMX request, from the same action.
/// </summary>
public static class Htmx
{
    public const string RequestHeader = "HX-Request";
    public const string TriggerHeader = "HX-Trigger";
    public const string RedirectHeader = "HX-Redirect";
    public const string RefreshHeader = "HX-Refresh";
    public const string RetargetHeader = "HX-Retarget";
    public const string ReswapHeader = "HX-Reswap";

    /// <summary>True when the request came from HTMX rather than a full browser navigation.</summary>
    public static bool IsHtmx(this HttpRequest request)
        => request.Headers.ContainsKey(RequestHeader);

    /// <summary>True when HTMX is replacing the whole page (hx-boost), so a layout is still wanted.</summary>
    public static bool IsHtmxBoosted(this HttpRequest request)
        => request.Headers.ContainsKey("HX-Boosted");

    /// <summary>Fires one or more client-side events after the swap, e.g. to refresh a table.</summary>
    public static void HxTrigger(this HttpResponse response, string eventName, object? detail = null)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?> { [eventName] = detail });
        response.Headers[TriggerHeader] = payload;
    }

    public static void HxTrigger(this HttpResponse response, IDictionary<string, object?> events)
        => response.Headers[TriggerHeader] = JsonSerializer.Serialize(events);

    /// <summary>Shows a toast and refreshes the page's main table in one response.</summary>
    public static void HxSaved(this HttpResponse response, string message, string level = "success")
        => response.HxTrigger(new Dictionary<string, object?>
        {
            ["aquatrack:toast"] = new { message, level },
            ["aquatrack:refresh"] = new { },
        });

    public static void HxToast(this HttpResponse response, string message, string level = "success")
        => response.HxTrigger("aquatrack:toast", new { message, level });

    /// <summary>Client-side redirect that works for an HTMX response, where a 302 would be swapped in.</summary>
    public static void HxRedirect(this HttpResponse response, string url)
        => response.Headers[RedirectHeader] = url;
}
