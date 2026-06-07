using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Features.Leaderboards.CaptureWealthSnapshots;
using System.Diagnostics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class WealthSnapshotRecurringJob(
    ISender sender,
    ILogger<WealthSnapshotRecurringJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await sender.Send(new CaptureWealthSnapshotsCommand());
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "wealth snapshot capture failed: {Code} - {Message}",
                result.Error?.Code,
                result.Error?.Message);
            return;
        }

        logger.LogInformation(
            "wealth snapshot capture completed. SnapshotsCaptured={SnapshotsCaptured}, CapturedAt={CapturedAt}, ElapsedMs={ElapsedMs}",
            result.Value?.SnapshotsCaptured,
            result.Value?.CapturedAt,
            stopwatch.ElapsedMilliseconds);
    }
}
