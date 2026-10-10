using AquaTrack.Application.Services;
using AquaTrack.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The alert feed. Alerts are produced by the anomaly detector rather than written by hand, so
/// this controller only reads, marks read, and triggers a re-scan.
/// </summary>
public class NotificationsController(INotificationService notifications) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await notifications.RefreshAsync(ct);

        ViewData["Title"] = "Alerts";
        ViewData["Subtitle"] = "What the system thinks needs your attention";

        return View(await notifications.GetRecentAsync(100, ct));
    }

    /// <summary>Just the bell, re-rendered on a poll so a new alert shows its badge.</summary>
    [HttpGet]
    public async Task<IActionResult> Bell(CancellationToken ct)
    {
        ViewData["UnreadCount"] = await notifications.GetUnreadCountAsync(ct);
        return PartialView("_Bell");
    }

    /// <summary>The dropdown contents, fetched when the bell is opened.</summary>
    [HttpGet]
    public async Task<IActionResult> Panel(CancellationToken ct)
        => PartialView("_Panel", await notifications.GetRecentAsync(8, ct));

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);

        Response.HxTrigger("aquatrack:refresh", new { });
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return Saved("All alerts marked as read.");
    }

    /// <summary>Re-runs the detectors on demand, for when the user wants an immediate check.</summary>
    [HttpPost]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raised = await notifications.RefreshAsync(ct);

        return Saved(raised == 0
            ? "Checked — nothing new to report."
            : $"{raised} new alert(s) raised.");
    }
}
