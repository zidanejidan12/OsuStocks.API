using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;

public sealed class SnapshotComparisonService : ISnapshotComparisonService
{
    // Minimum relative rank move (0.2%) before a RankChanged event is emitted. Tuned from 40h of live
    // snapshots: 0.2% yields a balanced two-directional market (~500 risers / ~340 fallers) while
    // filtering long-tail jitter so event/history volume stays bounded as the roster grows.
    private const decimal RankChangeThreshold = 0.002m;


    public SnapshotComparisonResult Compare(
        PlayerSnapshot? previousSnapshot,
        OsuUserProfile currentProfile,
        Guid trackedPlayerId,
        DateTimeOffset now)
    {
        if (previousSnapshot is null)
        {
            return new SnapshotComparisonResult([], false, false);
        }

        var events = new List<OsuDomainEvent>();

        if (currentProfile.CurrentPp > previousSnapshot.CurrentPp)
        {
            events.Add(new PpIncreased(
                trackedPlayerId,
                previousSnapshot.CurrentPp,
                currentProfile.CurrentPp,
                now));
        }

        if (currentProfile.TopScoreId.HasValue &&
            currentProfile.TopScoreId != previousSnapshot.TopScoreId)
        {
            events.Add(new TopPlayDetected(
                trackedPlayerId,
                previousSnapshot.TopScoreId,
                currentProfile.TopScoreId.Value,
                currentProfile.TopScorePp,
                now,
                currentProfile.TopScoreCoverUrl,
                currentProfile.TopScoreTitle,
                currentProfile.CurrentPp));
        }

        var isRankImproved = previousSnapshot.GlobalRank.HasValue
                             && currentProfile.GlobalRank.HasValue
                             && currentProfile.GlobalRank.Value < previousSnapshot.GlobalRank.Value;

        // Price meaningful rank moves bidirectionally (improve = up, drop = down). Skip jitter below
        // RankChangeThreshold (relative) so tiny ±rank wobble doesn't spam events or nudge the price.
        if (previousSnapshot.GlobalRank is { } previousRank && previousRank > 0
            && currentProfile.GlobalRank is { } currentRank && currentRank > 0
            && previousRank != currentRank)
        {
            var relativeChange = Math.Abs(previousRank - currentRank) / (decimal)previousRank;
            if (relativeChange >= RankChangeThreshold)
            {
                events.Add(new RankChanged(trackedPlayerId, previousRank, currentRank, now));
            }
        }

        var isInactive = previousSnapshot.CapturedAt <= now.AddDays(-14) &&
                         currentProfile.CurrentPp <= previousSnapshot.CurrentPp;

        if (isInactive)
        {
            events.Add(new PlayerInactive(trackedPlayerId, now));
        }

        return new SnapshotComparisonResult(events, isInactive, isRankImproved);
    }
}
