namespace AquaTrack.Web.Models;

public class ErrorViewModel
{
    /// <summary>Correlates what the user saw with the matching entry in the logs.</summary>
    public string? RequestId { get; init; }

    public string? Path { get; init; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

/// <summary>
/// A status-code page (404, 403 and friends). Kept separate from <see cref="ErrorViewModel"/>
/// because nothing has gone wrong on the server: the request was simply refused or not found, and
/// there is no request id worth showing the user.
/// </summary>
public class StatusErrorViewModel
{
    public int StatusCode { get; init; }
    public string Title { get; init; } = "Something went wrong";
    public string Message { get; init; } = string.Empty;

    public static StatusErrorViewModel For(int statusCode) => statusCode switch
    {
        404 => new StatusErrorViewModel
        {
            StatusCode = 404,
            Title = "Page not found",
            Message = "That page does not exist. It may have been removed, or the link may be wrong.",
        },
        403 => new StatusErrorViewModel
        {
            StatusCode = 403,
            Title = "You do not have access",
            Message = "Your role does not include this area. Ask an administrator if you need access.",
        },
        401 => new StatusErrorViewModel
        {
            StatusCode = 401,
            Title = "Please sign in",
            Message = "Your session may have expired. Sign in again to continue.",
        },
        400 => new StatusErrorViewModel
        {
            StatusCode = 400,
            Title = "That request could not be read",
            Message = "Something about the request was malformed. Try again from the page you started on.",
        },
        _ => new StatusErrorViewModel
        {
            StatusCode = statusCode,
            Title = "Something went wrong",
            Message = "The request could not be completed. Please try again.",
        },
    };
}
