using Hangfire;
using Microsoft.Extensions.Logging;
using OsuStocks.Domain.Repositories;
using System.Diagnostics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

/// <summary>
/// Prunes old market history so the append-only tables stay bounded as the tracked roster grows.
/// Rank-change pricing writes a stock_price_history + market_event row whenever a player's rank moves,
/// so these tables grow quickly. Current price lives on the stock itself, and charts/feeds only need a
/// recent window, so rows beyond the retention window are safe to delete.
/// </summary>
public sealed class MarketHistoryRetentionRecurringJob(
    IStockPriceHistoryRepository stockPriceHistoryRepository,
    IMarketEventRepository marketEventRepository,
    ILogger<MarketHistoryRetentionRecurringJob> logger)
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var cutoff = DateTimeOffset.UtcNow - RetentionWindow;

        var historyDeleted = await stockPriceHistoryRepository.DeleteOlderThanAsync(cutoff);
        var eventsDeleted = await marketEventRepository.DeleteOlderThanAsync(cutoff);

        stopwatch.Stop();

        logger.LogInformation(
            "market history retention prune completed. PriceHistoryDeleted={PriceHistoryDeleted}, " +
            "MarketEventsDeleted={MarketEventsDeleted}, Cutoff={Cutoff}, ElapsedMs={ElapsedMs}",
            historyDeleted,
            eventsDeleted,
            cutoff,
            stopwatch.ElapsedMilliseconds);
    }
}
