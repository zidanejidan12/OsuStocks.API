namespace OsuStocks.Application.Features.Leaderboards.Common;

internal static class LeaderboardPeriod
{
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";

    public static bool IsValid(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return true;
        }

        return period.Trim().ToLowerInvariant() is Daily or Weekly or Monthly;
    }

    public static string Normalize(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return Daily;
        }

        return period.Trim().ToLowerInvariant() switch
        {
            Weekly => Weekly,
            Monthly => Monthly,
            _ => Daily
        };
    }

    public static DateTimeOffset ToPeriodStart(string normalizedPeriod)
    {
        var now = DateTimeOffset.UtcNow;
        return normalizedPeriod switch
        {
            Weekly => now.AddDays(-7),
            Monthly => now.AddDays(-30),
            _ => now.AddDays(-1)
        };
    }
}
