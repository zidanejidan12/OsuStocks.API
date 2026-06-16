using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OsuStocks.Infrastructure.OsuIntegration.Telemetry;

/// <summary>
/// Passive osu! API health check: reports status from the recent success rate the
/// <see cref="OsuApiTelemetry"/> window already observed — it does NOT make an active call, so it
/// never burns rate-limit budget. No recent calls => Healthy (nothing to report).
/// </summary>
public sealed class OsuApiHealthCheck(OsuApiTelemetry telemetry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var (total, failures) = telemetry.HealthSnapshot();

        if (total == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("No osu! API calls in the last 5 minutes."));
        }

        var data = new Dictionary<string, object>
        {
            ["recentCalls"] = total,
            ["recentFailures"] = failures,
        };

        var failureRate = (double)failures / total;

        if (failureRate >= 0.5)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"osu! API failing: {failures}/{total} recent calls failed.", data: data));
        }

        if (failures > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"osu! API degraded: {failures}/{total} recent calls failed.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"osu! API healthy: {total} recent calls, no failures.", data: data));
    }
}
