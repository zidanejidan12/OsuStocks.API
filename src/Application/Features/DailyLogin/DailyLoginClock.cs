namespace OsuStocks.Application.Features.DailyLogin;

/// <summary>
/// Single source of the server "today" used by the daily-login feature. The reward day boundary is the
/// UTC calendar date — matching the codebase convention of <c>DateTimeOffset.UtcNow</c> everywhere — so it
/// must be derived via <c>.UtcDateTime</c>, never <c>.DateTime</c>/<c>.LocalDateTime</c> (which would be the
/// host's wall clock).
/// </summary>
public static class DailyLoginClock
{
    public static DateOnly ServerToday() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
}
