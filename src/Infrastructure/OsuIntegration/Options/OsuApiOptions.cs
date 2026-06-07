namespace OsuStocks.Infrastructure.OsuIntegration.Options;

public sealed class OsuApiOptions
{
    public const string SectionName = "OsuApi";

    public string BaseUrl { get; set; } = "https://osu.ppy.sh/api/v2/";

    /// <summary>
    /// Sustained osu! API request budget per minute. osu! asks third-party apps to stay well below
    /// their hard ceiling; 60/min is a conservative good-citizen default.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Maximum burst of requests allowed before throttling kicks in (token bucket capacity).
    /// </summary>
    public int BurstSize { get; set; } = 30;

    /// <summary>
    /// How many requests may wait for a permit before the limiter rejects them. Lets a tier's
    /// synchronization queue up rather than fail when it briefly outpaces the budget.
    /// </summary>
    public int RateLimitQueueLimit { get; set; } = 2000;
}
