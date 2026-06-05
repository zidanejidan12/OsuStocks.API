using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IMarketSettingsRepository
{
    Task<MarketSettings?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<MarketSettings?> GetCurrentForUpdateAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MarketSettings settings, CancellationToken cancellationToken = default);
    void Update(MarketSettings settings);
}
