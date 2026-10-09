namespace AquaTrack.Application.Common.Interfaces;

/// <summary>
/// Who is making the current request. Implemented in the web layer, which is the only place that
/// knows about HTTP, so application services can stamp audit data without depending on it.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

/// <summary>
/// The clock, behind an interface so date-sensitive logic (feed advice, forecasts, expiry alerts)
/// can be exercised at a chosen point in time rather than only "now".
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

/// <summary>Default implementation; replaced in tests to pin the date.</summary>
public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
