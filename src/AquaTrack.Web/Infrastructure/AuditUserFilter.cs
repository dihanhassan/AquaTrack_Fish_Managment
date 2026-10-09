using AquaTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AquaTrack.Web.Infrastructure;

/// <summary>
/// Hands the signed-in user's name to the DbContext for the duration of the request, so entity
/// audit columns are filled without every service needing to know about HTTP.
/// </summary>
public class AuditUserFilter(AquaTrackDbContext db) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var identity = context.HttpContext.User.Identity;
        if (identity?.IsAuthenticated == true)
            db.CurrentUser = identity.Name;
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
