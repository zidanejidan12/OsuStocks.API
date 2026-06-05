using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IPlayerSynchronizationService
{
    Task<PlayerSynchronizationSummary> SynchronizeTrackedPlayersAsync(
        TrackingTier? tier = null,
        CancellationToken cancellationToken = default);
}
