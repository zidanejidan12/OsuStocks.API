using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class HoldingRepository(AppDbContext dbContext) : IHoldingRepository
{
    public Task<Holding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Holdings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Holding?> GetByPortfolioAndStockAsync(
        Guid portfolioId,
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Holdings.AsNoTracking().FirstOrDefaultAsync(
            x => x.PortfolioId == portfolioId && x.StockId == stockId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Holding>> GetByPortfolioIdAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.PortfolioId == portfolioId)
            .OrderByDescending(x => x.Quantity)
            .ToListAsync(cancellationToken);
    }

    public Task<decimal> GetTotalQuantityByStockAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        return dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.StockId == stockId)
            .SumAsync(x => x.Quantity, cancellationToken);
    }

    public Task AddAsync(Holding holding, CancellationToken cancellationToken = default)
    {
        return dbContext.Holdings.AddAsync(holding, cancellationToken).AsTask();
    }

    public void Update(Holding holding)
    {
        var entry = dbContext.Entry(holding);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Holdings.Attach(holding);
            entry = dbContext.Entry(holding);
        }

        entry.State = EntityState.Modified;
        entry.Property(x => x.RowVersion).OriginalValue = holding.RowVersion;
    }

    public void Remove(Holding holding)
    {
        var entry = dbContext.Entry(holding);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Holdings.Attach(holding);
            entry = dbContext.Entry(holding);
        }

        entry.Property(x => x.RowVersion).OriginalValue = holding.RowVersion;
        dbContext.Holdings.Remove(holding);
    }
}
