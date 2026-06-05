using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class OsuSynchronizationRecurringJob(
    ISender sender,
    ILogger<OsuSynchronizationRecurringJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunTierAsync(TrackingTier tier)
    {
        var result = await sender.Send(new SynchronizeTrackedPlayersCommand(tier));

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "osu synchronization for tier {Tier} failed: {Code} - {Message}",
                tier,
                result.Error?.Code,
                result.Error?.Message);
            return;
        }

        logger.LogInformation(
            "osu synchronization tier {Tier} completed. TrackedPlayers={TrackedPlayers}, Snapshots={SnapshotsCreated}, Events={EventsDetected}, RankImprovements={RankImprovementsDetected}",
            tier,
            result.Value?.TrackedPlayers,
            result.Value?.SnapshotsCreated,
            result.Value?.EventsDetected,
            result.Value?.RankImprovementsDetected);
    }
}
