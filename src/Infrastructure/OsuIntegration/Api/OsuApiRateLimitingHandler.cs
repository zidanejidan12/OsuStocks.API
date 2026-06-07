namespace OsuStocks.Infrastructure.OsuIntegration.Api;

/// <summary>
/// Acquires a permit from the shared <see cref="OsuApiRateLimiter"/> before every osu! API request.
/// Throttling prevents us from tripping osu!'s rate limit; if a 429 still slips through (e.g. the
/// app's quota is shared elsewhere) it surfaces to the caller and Hangfire's job retry handles it.
/// </summary>
internal sealed class OsuApiRateLimitingHandler(OsuApiRateLimiter rateLimiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.Limiter.AcquireAsync(1, cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new HttpRequestException(
                "osu! API rate limiter rejected the request: the throttle queue is full.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
