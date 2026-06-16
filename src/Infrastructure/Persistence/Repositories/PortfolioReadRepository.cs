using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class PortfolioReadRepository(AppDbContext dbContext) : IPortfolioReadRepository
{
    public async Task<IReadOnlyList<PortfolioHoldingSummaryReadModel>> GetPortfolioSummaryHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Sort client-side after a single SELECT: a user's holdings are few, and ordering by the
        // decimal Quantity in SQL isn't supported by the SQLite provider used in query-count tests
        // (Postgres handles it fine either way).
        var holdings = await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.Portfolio.UserId == userId)
            .Select(x => new PortfolioHoldingSummaryReadModel(
                x.Id,
                x.StockId,
                x.Stock.TrackedPlayer.Username,
                x.Quantity,
                x.AveragePrice,
                x.Stock.CurrentPrice,
                x.AveragePrice * x.Quantity,
                x.Stock.CurrentPrice * x.Quantity,
                (x.Stock.CurrentPrice * x.Quantity) - (x.AveragePrice * x.Quantity),
                x.Stock.TrackedPlayer.AvatarUrl))
            .ToListAsync(cancellationToken);

        return holdings.OrderByDescending(x => x.Quantity).ToList();
    }

    public async Task<IReadOnlyList<HoldingReadModel>> GetHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var holdings = await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.Portfolio.UserId == userId)
            .Select(x => new HoldingReadModel(
                x.Id,
                x.StockId,
                x.Stock.TrackedPlayer.Username,
                x.Quantity,
                x.AveragePrice,
                x.Stock.CurrentPrice,
                x.Stock.TrackedPlayer.AvatarUrl))
            .ToListAsync(cancellationToken);

        return holdings.OrderByDescending(x => x.Quantity).ToList();
    }
}
