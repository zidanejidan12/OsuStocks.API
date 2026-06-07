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
        return await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.Portfolio.UserId == userId)
            .OrderByDescending(x => x.Quantity)
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
    }

    public async Task<IReadOnlyList<HoldingReadModel>> GetHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Holdings
            .AsNoTracking()
            .Where(x => x.Portfolio.UserId == userId)
            .OrderByDescending(x => x.Quantity)
            .Select(x => new HoldingReadModel(
                x.Id,
                x.StockId,
                x.Stock.TrackedPlayer.Username,
                x.Quantity,
                x.AveragePrice,
                x.Stock.CurrentPrice,
                x.Stock.TrackedPlayer.AvatarUrl))
            .ToListAsync(cancellationToken);
    }
}
