using Hangfire;
using Microsoft.Extensions.Logging;
using OsuStocks.Domain.Repositories;
using System.Diagnostics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

/// <summary>
/// Prunes old player snapshots so the table stays bounded as the tracked roster grows. Only the most
/// recent snapshot per player is needed for sync comparison, and every player syncs at least hourly,
/// so rows beyond the retention window are safe to delete.
/// </summary>
public sealed class SnapshotRetentionRecurringJob(
    IPlayerSnapshotRepository playerSnapshotRepository,
    ILogger<SnapshotRetentionRecurringJob> logger)
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(14);

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var cutoff = DateTimeOffset.UtcNow - RetentionWindow;
        var deleted = await playerSnapshotRepository.DeleteOlderThanAsync(cutoff);
        stopwatch.Stop();

        logger.LogInformation(
            "snapshot retention prune completed. Deleted={Deleted}, Cutoff={Cutoff}, ElapsedMs={ElapsedMs}",
            deleted,
            cutoff,
            stopwatch.ElapsedMilliseconds);
    }
}
