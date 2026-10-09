using AquaTrack.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Shared behaviour for every signed-in screen: the HTMX response helpers and the
/// "partial for HTMX, full page otherwise" convention that keeps each action single-purpose.
/// </summary>
[Authorize]
public abstract class AppController : Controller
{
    protected bool IsHtmx => Request.IsHtmx();

    /// <summary>
    /// Returns the partial alone to an HTMX request and the full page to a normal navigation,
    /// so every list URL is shareable and still swaps cheaply in the browser.
    /// </summary>
    protected IActionResult PartialOrView(string partialName, object model, string? viewName = null)
        => IsHtmx && !Request.IsHtmxBoosted()
            ? PartialView(partialName, model)
            : View(viewName ?? "Index", model);

    /// <summary>Signals a successful write: toast plus a refresh of the calling page's table.</summary>
    protected IActionResult Saved(string message)
    {
        Response.HxSaved(message);
        return NoContent();
    }

    /// <summary>Closes the modal and sends the browser somewhere else (used after create-and-open).</summary>
    protected IActionResult SavedAndRedirect(string message, string url)
    {
        Response.HxToast(message);
        Response.HxRedirect(url);
        return NoContent();
    }

    /// <summary>Reports a failure without discarding what the user typed.</summary>
    protected IActionResult Failed(string message)
    {
        Response.HxToast(message, "danger");
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return NoContent();
    }

    protected void Toast(string message, string level = "success")
        => TempData["Toast"] = $"{level}|{message}";

    /// <summary>Flattens ModelState into a single line, for toasting a form error from a non-form context.</summary>
    protected string ModelStateSummary()
        => string.Join(" ", ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)));
}
