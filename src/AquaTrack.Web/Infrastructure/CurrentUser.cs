using System.Security.Claims;
using AquaTrack.Application.Common.Interfaces;

namespace AquaTrack.Web.Infrastructure;

/// <summary>
/// Supplies the signed-in user to the layers below. HTTP lives only in the web project, so this is
/// the one place that reads <see cref="HttpContext"/> on the application layer's behalf.
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
