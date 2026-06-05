using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;

public sealed class SnapshotComparisonService : ISnapshotComparisonService
{
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
                now));
        }

        var isRankImproved = previousSnapshot.GlobalRank.HasValue
                             && currentProfile.GlobalRank.HasValue
                             && currentProfile.GlobalRank.Value < previousSnapshot.GlobalRank.Value;

        var isInactive = previousSnapshot.CapturedAt <= now.AddDays(-14) &&
                         currentProfile.CurrentPp <= previousSnapshot.CurrentPp;

        if (isInactive)
        {
            events.Add(new PlayerInactive(trackedPlayerId, now));
        }

        return new SnapshotComparisonResult(events, isInactive, isRankImproved);
    }
}
