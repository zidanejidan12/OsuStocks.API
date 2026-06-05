using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;
using OsuStocks.Domain.Common.Enums;
using System.Diagnostics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class OsuSynchronizationRecurringJob(
    ISender sender,
    ILogger<OsuSynchronizationRecurringJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public Task RunTier1Async()
    {
        return RunTierAsync(TrackingTier.Tier1);
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public Task RunTier2Async()
    {
        return RunTierAsync(TrackingTier.Tier2);
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public Task RunTier3Async()
    {
        return RunTierAsync(TrackingTier.Tier3);
    }

    private async Task RunTierAsync(TrackingTier tier)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await sender.Send(new SynchronizeTrackedPlayersCommand(tier));
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "osu synchronization for tier {Tier} failed: {Code} - {Message}",
                tier,
                result.Error?.Code,
                result.Error?.Message);
            OsuSynchronizationRecurringJobMetrics.RecordCompletion(tier, false, stopwatch.Elapsed, null);
            return;
        }

        logger.LogInformation(
            "osu synchronization tier {Tier} completed. TrackedPlayers={TrackedPlayers}, Snapshots={SnapshotsCreated}, Events={EventsDetected}, RankImprovements={RankImprovementsDetected}",
            tier,
            result.Value?.TrackedPlayers,
            result.Value?.SnapshotsCreated,
            result.Value?.EventsDetected,
            result.Value?.RankImprovementsDetected);
        OsuSynchronizationRecurringJobMetrics.RecordCompletion(tier, true, stopwatch.Elapsed, result.Value);
    }
}
