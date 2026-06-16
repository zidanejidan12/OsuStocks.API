using System.Diagnostics;
using OsuStocks.Infrastructure.OsuIntegration.Telemetry;

namespace OsuStocks.Infrastructure.OsuIntegration.Api;

/// <summary>
/// Acquires a permit from the shared <see cref="OsuApiRateLimiter"/> before every osu! API request.
/// Throttling prevents us from tripping osu!'s rate limit; if a 429 still slips through (e.g. the
/// app's quota is shared elsewhere) it surfaces to the caller and Hangfire's job retry handles it.
/// Every call is recorded to <see cref="OsuApiTelemetry"/> (outcome, latency, rate-limit wait).
/// </summary>
internal sealed class OsuApiRateLimitingHandler(
    OsuApiRateLimiter rateLimiter,
    OsuApiTelemetry telemetry) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var waitStart = Stopwatch.GetTimestamp();
        using var lease = await rateLimiter.Limiter.AcquireAsync(1, cancellationToken);
        telemetry.RecordPermitWait(Stopwatch.GetElapsedTime(waitStart).TotalMilliseconds);

        if (!lease.IsAcquired)
        {
            telemetry.RecordRequest("rejected", statusCode: null, durationMs: 0);
            throw new HttpRequestException(
                "osu! API rate limiter rejected the request: the throttle queue is full.");
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var code = (int)response.StatusCode;
            var outcome = code switch
            {
                429 => "rate_limited",
                >= 500 => "server_error",
                >= 400 => "client_error",
                _ => "success",
            };
            telemetry.RecordRequest(outcome, code, elapsed);

            return response;
        }
        catch (OperationCanceledException)
        {
            // Shutdown/cancellation is not an osu! API failure — don't count it.
            throw;
        }
        catch (Exception)
        {
            telemetry.RecordRequest("exception", statusCode: null, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }
}
