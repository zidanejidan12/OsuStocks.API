using Microsoft.Extensions.Options;
using OsuStocks.Infrastructure.OsuIntegration.Options;
using System.Threading.RateLimiting;

namespace OsuStocks.Infrastructure.OsuIntegration.Api;

/// <summary>
/// Process-wide token-bucket limiter shared by every outbound osu! API call, so the application
/// cannot exceed osu!'s request budget no matter how many synchronization jobs run concurrently.
/// Registered as a singleton; the <see cref="OsuApiRateLimitingHandler"/> acquires a permit per request.
/// </summary>
public sealed class OsuApiRateLimiter : IAsyncDisposable
{
    public OsuApiRateLimiter(IOptions<OsuApiOptions> options)
    {
        var value = options.Value;

        // Replenish every second so the budget is spread smoothly across the minute rather than
        // refilling in one large chunk that would permit a thundering herd at the top of each minute.
        var tokensPerSecond = Math.Max(1, value.RequestsPerMinute / 60);

        Limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, value.BurstSize),
            TokensPerPeriod = tokensPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = Math.Max(0, value.RateLimitQueueLimit),
            AutoReplenishment = true
        });
    }

    public RateLimiter Limiter { get; }

    public ValueTask DisposeAsync()
    {
        return Limiter.DisposeAsync();
    }
}
