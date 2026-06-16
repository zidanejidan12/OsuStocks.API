using System.Diagnostics;
using System.Diagnostics.Metrics;
using OsuStocks.Infrastructure.OsuIntegration.Api;

namespace OsuStocks.Infrastructure.OsuIntegration.Telemetry;

/// <summary>
/// Records metrics for every outbound osu! API call (count by outcome, latency, rate-limit
/// saturation) on a dedicated <see cref="Meter"/>, and keeps a small rolling window of recent
/// outcomes that the passive osu-api health check reads. Registered as a singleton.
/// </summary>
public sealed class OsuApiTelemetry : IDisposable
{
    public const string MeterName = "OsuStocks.OsuApi";

    private static readonly TimeSpan HealthWindow = TimeSpan.FromMinutes(5);
    private const int MaxRecent = 256;

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _duration;
    private readonly Histogram<double> _permitWait;

    private readonly object _gate = new();
    private readonly Queue<(DateTimeOffset At, bool Ok)> _recent = new();

    public OsuApiTelemetry(OsuApiRateLimiter rateLimiter)
    {
        _meter = new Meter(MeterName);
        _requests = _meter.CreateCounter<long>(
            "osu_api.requests", unit: "{request}", description: "osu! API requests by outcome.");
        _duration = _meter.CreateHistogram<double>(
            "osu_api.request.duration", unit: "ms", description: "osu! API request latency.");
        _permitWait = _meter.CreateHistogram<double>(
            "osu_api.rate_limit.wait", unit: "ms", description: "Time spent waiting for a rate-limit permit.");

        // Saturation gauge: how much of the token-bucket budget is currently available.
        _meter.CreateObservableGauge(
            "osu_api.rate_limit.available_permits",
            () => rateLimiter.Limiter.GetStatistics()?.CurrentAvailablePermits ?? 0,
            unit: "{permit}",
            description: "Currently available osu! API rate-limit permits.");
    }

    public Meter Meter => _meter;

    public void RecordRequest(string outcome, int? statusCode, double durationMs)
    {
        var tags = new TagList { { "outcome", outcome } };
        if (statusCode is not null)
        {
            tags.Add("status_code", statusCode.Value);
        }

        _requests.Add(1, tags);
        _duration.Record(durationMs, tags);

        var ok = outcome == "success";
        lock (_gate)
        {
            _recent.Enqueue((DateTimeOffset.UtcNow, ok));
            while (_recent.Count > MaxRecent)
            {
                _recent.Dequeue();
            }
        }
    }

    public void RecordPermitWait(double waitMs) => _permitWait.Record(waitMs);

    /// <summary>Recent (last 5 min) call total and failure count, for the health check.</summary>
    public (int Total, int Failures) HealthSnapshot()
    {
        var cutoff = DateTimeOffset.UtcNow - HealthWindow;
        lock (_gate)
        {
            while (_recent.Count > 0 && _recent.Peek().At < cutoff)
            {
                _recent.Dequeue();
            }

            var total = _recent.Count;
            var failures = 0;
            foreach (var entry in _recent)
            {
                if (!entry.Ok)
                {
                    failures++;
                }
            }

            return (total, failures);
        }
    }

    public void Dispose() => _meter.Dispose();
}
