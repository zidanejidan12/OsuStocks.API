using OsuStocks.Domain.OsuIntegration.Models;

namespace OsuStocks.Domain.OsuIntegration.Interfaces;

public interface IPlayerSynchronizationService
{
    Task<PlayerSynchronizationSummary> SynchronizeTrackedPlayersAsync(CancellationToken cancellationToken = default);
}
