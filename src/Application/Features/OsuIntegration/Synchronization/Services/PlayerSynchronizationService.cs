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

        var snapshotsCreated = 0;
        var eventsDetected = 0;
        var rankImprovementsDetected = 0;

        foreach (var trackedPlayer in trackedPlayers)
        {
            var now = DateTimeOffset.UtcNow;

            var latestProfile = await osuApiClient.GetUserAsync(
                trackedPlayer.OsuUserId,
                osuToken.AccessToken,
                cancellationToken);

            var previousSnapshot = await playerSnapshotRepository
                .GetLatestByTrackedPlayerIdAsync(trackedPlayer.Id, cancellationToken);

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

            if (comparison.Events.Count > 0)
            {
                var playerStock = await playerStockRepository
                    .GetByTrackedPlayerIdAsync(trackedPlayer.Id, cancellationToken);

                if (playerStock is not null)
                {
                    foreach (var domainEvent in comparison.Events)
                    {
                        var marketEvent = new MarketEvent
                        {
                            Id = Guid.NewGuid(),
                            StockId = playerStock.Id,
                            EventType = domainEvent.EventType,
                            Payload = JsonSerializer.Serialize(domainEvent),
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

                eventsDetected += comparison.Events.Count;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlayerSynchronizationSummary(
            trackedPlayers.Count,
            snapshotsCreated,
            eventsDetected,
            rankImprovementsDetected);
    }
}
