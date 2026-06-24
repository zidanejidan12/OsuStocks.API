using System.Net;
using Microsoft.Extensions.Logging;

namespace OsuStocks.Infrastructure.OsuIntegration.Api;

/// <summary>
/// Retries transient osu! responses (429 and 5xx) with backoff before giving up, so a brief osu!-side
/// throttle or hiccup self-heals instead of failing a login or a sync job. Registered as the OUTER
/// handler (ahead of <see cref="OsuApiRateLimitingHandler"/>) so each retry re-acquires a rate-limit
/// permit rather than bypassing the throttle. On a 429 it honors the <c>Retry-After</c> header; queue-full
/// rejections from the limiter surface as exceptions and are intentionally NOT retried (that would only
/// add pressure to a full queue).
/// </summary>
internal sealed class OsuApiRetryHandler(ILogger<OsuApiRetryHandler> logger) : DelegatingHandler
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Buffer the body once so the request can be safely re-issued: an HttpRequestMessage (and its
        // content stream) can only be sent a single time, so each attempt gets a fresh clone.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = request.Content?.Headers.ToList();

        HttpResponseMessage? response = null;
        for (var attempt = 1; ; attempt++)
        {
            response?.Dispose();
            using var attemptRequest = CloneRequest(request, body, contentHeaders);
            response = await base.SendAsync(attemptRequest, cancellationToken);

            if (attempt >= MaxAttempts || !IsTransient(response.StatusCode))
            {
                return response;
            }

            var delay = GetRetryDelay(response, attempt);
            logger.LogWarning(
                "osu! API returned {StatusCode} for {Method} {Url}; retry {Attempt}/{MaxRetries} in {DelayMs}ms.",
                (int)response.StatusCode,
                request.Method,
                request.RequestUri,
                attempt,
                MaxAttempts - 1,
                (int)delay.TotalMilliseconds);

            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests        // 429
            or HttpStatusCode.InternalServerError           // 500
            or HttpStatusCode.BadGateway                    // 502
            or HttpStatusCode.ServiceUnavailable            // 503
            or HttpStatusCode.GatewayTimeout;               // 504

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        // Prefer osu!'s own Retry-After hint when present (it knows exactly how long the budget is gone).
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return Cap(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var until = date - DateTimeOffset.UtcNow;
            if (until > TimeSpan.Zero)
            {
                return Cap(until);
            }
        }

        // Otherwise exponential backoff with jitter: 0.5s, 1s, 2s ... plus up to 250ms of spread so
        // concurrent callers don't retry in lockstep.
        var backoff = BaseDelay * Math.Pow(2, attempt - 1);
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        return Cap(backoff + jitter);
    }

    private static TimeSpan Cap(TimeSpan delay) => delay > MaxDelay ? MaxDelay : delay;

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage source,
        byte[]? body,
        List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            if (contentHeaders is not null)
            {
                clone.Content.Headers.Clear();
                foreach (var header in contentHeaders)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return clone;
    }
}
