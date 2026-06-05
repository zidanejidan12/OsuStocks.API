using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class MarketSettingsRepository(AppDbContext dbContext) : IMarketSettingsRepository
{
    public Task<MarketSettings?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.MarketSettings
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<MarketSettings?> GetCurrentForUpdateAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.MarketSettings
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(MarketSettings settings, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketSettings.AddAsync(settings, cancellationToken).AsTask();
    }

    public void Update(MarketSettings settings)
    {
        dbContext.MarketSettings.Update(settings);
    }
}
