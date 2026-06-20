using Hangfire;
using Microsoft.Extensions.Logging;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Observability;

namespace OsuStocks.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically aggregates the credit economy (circulating / minted / burned) and pushes it into the
/// <see cref="EconomyMetrics"/> gauges. Runs on the worker so the gauges export from a single process.
/// </summary>
public sealed class EconomyMetricsRecurringJob(
    IEconomyReadRepository economyReadRepository,
    EconomyMetrics economyMetrics,
    ILogger<EconomyMetricsRecurringJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunAsync()
    {
        var snapshot = await economyReadRepository.GetSnapshotAsync();
        economyMetrics.Update(snapshot.CirculatingCredits, snapshot.MintedCredits, snapshot.BurnedCredits);

        logger.LogInformation(
            "Economy snapshot: circulating={Circulating}, minted={Minted}, burned={Burned}, net issuance={Net}",
            snapshot.CirculatingCredits,
            snapshot.MintedCredits,
            snapshot.BurnedCredits,
            snapshot.MintedCredits - snapshot.BurnedCredits);
    }
}
