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

    public Task AddAsync(Holding holding, CancellationToken cancellationToken = default)
    {
        return dbContext.Holdings.AddAsync(holding, cancellationToken).AsTask();
    }

    public void Update(Holding holding)
    {
        dbContext.Holdings.Update(holding);
    }

    public void Remove(Holding holding)
    {
        dbContext.Holdings.Remove(holding);
    }
}
