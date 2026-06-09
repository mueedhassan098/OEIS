namespace OEIS.Application.Common;

/// <summary>
/// Application business timezone (UTC+05:00).
/// </summary>
public static class AppTime
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(5);

    /// <summary>Current date and time in UTC+05:00.</summary>
    public static DateTime Now => DateTime.UtcNow.Add(UtcOffset);

    /// <summary>Current calendar date in UTC+05:00.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);
}
