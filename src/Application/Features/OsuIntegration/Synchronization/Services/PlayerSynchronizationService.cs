using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Domain.Repositories;
using System.Text.Json;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;

public sealed class PlayerSynchronizationService(
    IOsuOAuthService osuOAuthService,
    IOsuApiClient osuApiClient,
    ISnapshotComparisonService snapshotComparisonService,
    ITrackedPlayerRepository trackedPlayerRepository,
    IPlayerSnapshotRepository playerSnapshotRepository,
    IPlayerStockRepository playerStockRepository,
    IMarketEventRepository marketEventRepository,
    IApplicationDbContext dbContext,
    IPublisher? publisher = null)
    : IPlayerSynchronizationService
{
    // Caps in-flight osu! API requests during the fetch phase. The process-wide OsuApiRateLimiter
    // is the authoritative rate guard (requests/minute); this just bounds concurrent sockets so we
    // saturate that budget without opening hundreds of connections at once for a large tier.
    private const int MaxFetchConcurrency = 10;

    // How many of a player's best plays to inspect each cycle. Any of these that were set since the
    // previous snapshot is surfaced as a top play (so the stock page shows recent beatmap-rich plays,
    // not only changes to the #1).
    private const int TopScoreFetchCount = 10;

    public async Task<PlayerSynchronizationSummary> SynchronizeTrackedPlayersAsync(
        TrackingTier? tier = null,
        CancellationToken cancellationToken = default)
    {
        var trackedPlayers = tier.HasValue
            ? await trackedPlayerRepository.GetActiveByTierAsync(tier.Value, cancellationToken)
            : await trackedPlayerRepository.GetActiveAsync(cancellationToken);

        if (trackedPlayers.Count == 0)
        {
            return new PlayerSynchronizationSummary(0, 0, 0, 0);
        }

        var osuToken = await osuOAuthService.GetClientCredentialsTokenAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // Batch-load the latest snapshot per player up front so the parallel fetch phase needs no
        // DbContext access (EF Core is not thread-safe — all persistence is serialized in phase 2).
        var previousSnapshots = await playerSnapshotRepository.GetLatestByTrackedPlayerIdsAsync(
            trackedPlayers.Select(player => player.Id).ToList(),
            cancellationToken);

        // Phase 1 — fetch osu! profiles concurrently (bounded). Each player's failure (restricted /
        // deleted account, transient API error) is isolated so it cannot abort the whole tier; the
        // next cycle retries it. No shared mutable state and no DbContext use here.
        using var concurrency = new SemaphoreSlim(MaxFetchConcurrency);

        var fetchTasks = trackedPlayers.Select(async trackedPlayer =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                previousSnapshots.TryGetValue(trackedPlayer.Id, out var previousSnapshot);

                var latestProfile = await osuApiClient.GetUserAsync(
                    trackedPlayer.OsuUserId,
                    osuToken.AccessToken,
                    includeTopScore: false,
                    cancellationToken);

                // A new top play always raises total pp, so when pp is unchanged the previous top
                // score still stands and we can skip the extra best-scores API call.
                IReadOnlyList<OsuTopScore> topScores = [];
                if (previousSnapshot is null || latestProfile.CurrentPp != previousSnapshot.CurrentPp)
                {
                    topScores = await osuApiClient.GetTopScoresAsync(
                        trackedPlayer.OsuUserId,
                        osuToken.AccessToken,
                        TopScoreFetchCount,
                        cancellationToken);

                    var topScore = topScores.Count > 0 ? topScores[0] : null;
                    latestProfile = latestProfile with
                    {
                        TopScoreId = topScore?.Id,
                        TopScorePp = topScore?.Pp,
                        TopScoreCoverUrl = topScore?.CoverUrl,
                        TopScoreTitle = topScore?.Title
                    };
                }
                else
                {
                    latestProfile = latestProfile with
                    {
                        TopScoreId = previousSnapshot.TopScoreId,
                        TopScorePp = previousSnapshot.TopScorePp
                    };
                }

                return new PlayerFetchResult(trackedPlayer, previousSnapshot, latestProfile, topScores);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Skip this player for this cycle; the others still sync.
                return null;
            }
            finally
            {
                concurrency.Release();
            }
        });

        var fetched = (await Task.WhenAll(fetchTasks))
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();

        // Phase 2 — persist sequentially on the single scoped DbContext.
        var snapshotsCreated = 0;
        var eventsDetected = 0;
        var rankImprovementsDetected = 0;

        foreach (var (trackedPlayer, previousSnapshot, latestProfile, topScores) in fetched)
        {
            // Keep the player's display fields fresh from osu! (avatar + country flag). The tracked
            // entity is read AsNoTracking, so persist via the repository's Update when they change.
            if (trackedPlayer.AvatarUrl != latestProfile.AvatarUrl ||
                trackedPlayer.CountryCode != latestProfile.CountryCode)
            {
                trackedPlayer.AvatarUrl = latestProfile.AvatarUrl;
                trackedPlayer.CountryCode = latestProfile.CountryCode;
                trackedPlayer.UpdatedAt = now;
                trackedPlayer.UpdatedBy = "sync";
                trackedPlayerRepository.Update(trackedPlayer);
            }

            var comparison = snapshotComparisonService.Compare(
                previousSnapshot,
                latestProfile,
                trackedPlayer.Id,
                now);

            var newSnapshot = new PlayerSnapshot
            {
                Id = Guid.NewGuid(),
                TrackedPlayerId = trackedPlayer.Id,
                CurrentPp = latestProfile.CurrentPp,
                GlobalRank = latestProfile.GlobalRank,
                TopScoreId = latestProfile.TopScoreId,
                TopScorePp = latestProfile.TopScorePp,
                CapturedAt = now
            };

            await playerSnapshotRepository.AddAsync(newSnapshot, cancellationToken);
            snapshotsCreated++;

            if (comparison.IsRankImproved)
            {
                rankImprovementsDetected++;
            }

            // Compare already flags a change to the #1 play. Surface every other newly-set play in
            // the player's top-N (by score submission time) too, so each beatmap-rich play that moved
            // the stock appears — not only changes to the single best score. Skip the #1 to avoid
            // double-counting it with Compare's event.
            var events = new List<OsuDomainEvent>(comparison.Events);
            if (previousSnapshot is not null)
            {
                foreach (var score in topScores)
                {
                    if (score.SetAt is { } setAt
                        && setAt > previousSnapshot.CapturedAt
                        && score.Id != latestProfile.TopScoreId)
                    {
                        events.Add(new TopPlayDetected(
                            trackedPlayer.Id,
                            null,
                            score.Id,
                            score.Pp,
                            now,
                            score.CoverUrl,
                            score.Title));
                    }
                }
            }

            if (events.Count > 0)
            {
                var playerStock = await playerStockRepository
                    .GetByTrackedPlayerIdAsync(trackedPlayer.Id, cancellationToken);

                if (playerStock is not null)
                {
                    foreach (var domainEvent in events)
                    {
                        var marketEvent = new MarketEvent
                        {
                            Id = Guid.NewGuid(),
                            StockId = playerStock.Id,
                            EventType = domainEvent.EventType,
                            // Serialize the runtime type, not the OsuDomainEvent base — otherwise
                            // System.Text.Json drops every derived field (score id, pp, cover, title).
                            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                            CreatedAt = now
                        };

                        await marketEventRepository.AddAsync(marketEvent, cancellationToken);

                        if (publisher is not null)
                        {
                            switch (domainEvent)
                            {
                                case PpIncreased ppIncreased:
                                    await publisher.Publish(new PpIncreasedNotification(ppIncreased), cancellationToken);
                                    break;
                                case TopPlayDetected topPlayDetected:
                                    await publisher.Publish(new TopPlayDetectedNotification(topPlayDetected), cancellationToken);
                                    break;
                                case PlayerInactive inactive:
                                    await publisher.Publish(new PlayerInactiveNotification(inactive), cancellationToken);
                                    break;
                            }
                        }
                    }
                }

                eventsDetected += events.Count;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlayerSynchronizationSummary(
            trackedPlayers.Count,
            snapshotsCreated,
            eventsDetected,
            rankImprovementsDetected);
    }

    private sealed record PlayerFetchResult(
        TrackedPlayer TrackedPlayer,
        PlayerSnapshot? PreviousSnapshot,
        OsuUserProfile LatestProfile,
        IReadOnlyList<OsuTopScore> TopScores);
}
