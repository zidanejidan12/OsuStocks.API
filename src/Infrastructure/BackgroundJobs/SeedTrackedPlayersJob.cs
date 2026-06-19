using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;
using System.Diagnostics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire wrapper for the (long-running) top-player seed. Enqueued on demand by the admin endpoint
/// and executed on the worker. The seed is idempotent, so retries are unnecessary — disabled to avoid
/// re-walking thousands of ranking entries after a transient blip.
/// </summary>
public sealed class SeedTrackedPlayersJob(
    ISender sender,
    ILogger<SeedTrackedPlayersJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunAsync(int count, string actor)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await sender.Send(new SeedTopPlayersCommand(count, actor));
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "Top-player seed (count={Count}) failed: {Code} - {Message}",
                count, result.Error?.Code, result.Error?.Message);
            return;
        }

        logger.LogInformation(
            "Top-player seed complete in {Elapsed}. Requested={Requested}, Fetched={Fetched}, Added={Added}, Skipped={Skipped}.",
            stopwatch.Elapsed,
            result.Value?.Requested,
            result.Value?.Fetched,
            result.Value?.Added,
            result.Value?.Skipped);
    }
}
