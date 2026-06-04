using OsuStocks.Domain.Entities;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface ISnapshotComparisonService
{
    SnapshotComparisonResult Compare(
        PlayerSnapshot? previousSnapshot,
        OsuUserProfile currentProfile,
        Guid trackedPlayerId,
        DateTimeOffset now);
}
