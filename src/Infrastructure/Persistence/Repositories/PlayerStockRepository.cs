using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class PlayerStockRepository(AppDbContext dbContext) : IPlayerStockRepository
{
    public Task<PlayerStock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerStocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<PlayerStock?> GetByTrackedPlayerIdAsync(Guid trackedPlayerId, CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerStocks.AsNoTracking().FirstOrDefaultAsync(x => x.TrackedPlayerId == trackedPlayerId, cancellationToken);
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
        dbContext.PlayerStocks.Update(playerStock);
    }
}
