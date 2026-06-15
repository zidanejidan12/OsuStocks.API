using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class PlayerStockRepository(AppDbContext dbContext) : IPlayerStockRepository
{
    // Tracked (not AsNoTracking): these load a stock that the caller then mutates and Update()s.
    // When several price-affecting events hit the same stock in one DbContext scope (e.g. a new top
    // play raises pp, firing both PpIncreased and TopPlayDetected), AsNoTracking returned a fresh
    // instance each time and the second Update() threw "another instance with the same key is already
    // being tracked". Tracking makes EF's identity map return the same instance, so repeated
    // fetch+Update in one scope is safe and concurrency tokens stay in sync.
    public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerStocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerStocks.FirstOrDefaultAsync(x => x.TrackedPlayerId == trackedPlayerId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerStock>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayerStocks
            .AsNoTracking()
            .OrderByDescending(x => x.CurrentPrice)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(PlayerStock playerStock, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerStocks.AddAsync(playerStock, cancellationToken).AsTask();
    }

    public void Update(PlayerStock playerStock)
    {
        var entry = dbContext.Entry(playerStock);
        if (entry.State == EntityState.Detached)
        {
            dbContext.PlayerStocks.Attach(playerStock);
            entry = dbContext.Entry(playerStock);
        }

        entry.State = EntityState.Modified;
        entry.Property(x => x.RowVersion).OriginalValue = playerStock.RowVersion;
    }
}
