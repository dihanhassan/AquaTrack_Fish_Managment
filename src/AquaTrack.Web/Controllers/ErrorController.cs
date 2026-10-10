using System.Diagnostics;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The production error page. It shows the request id so a report from a user can be matched to a
/// log entry, and nothing else about the failure.
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController(ILogger<ErrorController> logger) : Controller
{
    [Route("Error")]
    public IActionResult Index()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (feature?.Error is { } error)
            logger.LogError(error, "Unhandled error on {Path} (request {RequestId})", feature.Path, requestId);

        ViewData["Title"] = "Something went wrong";

        return View(new ErrorViewModel
        {
            RequestId = requestId,
            Path = feature?.Path,
        });
    }

    /// <summary>
    /// Status codes that are not server failures: a wrong URL, a refused role, an expired session.
    /// Re-executed onto this action so the user keeps the normal layout instead of a bare page.
    /// </summary>
    [Route("Error/{statusCode:int}")]
    public IActionResult Status(int statusCode)
    {
        Response.StatusCode = statusCode;
        return View(StatusErrorViewModel.For(statusCode));
    }
}
