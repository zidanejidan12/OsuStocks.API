using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class PortfolioRepository(AppDbContext dbContext) : IPortfolioRepository
{
    public Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Portfolio?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Portfolios.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken = default)
    {
        return dbContext.Portfolios.AddAsync(portfolio, cancellationToken).AsTask();
    }

    public void Update(Portfolio portfolio)
    {
        dbContext.Portfolios.Update(portfolio);
    }
}
