using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryPortfolioReadRepository(
    InMemoryPortfolioRepository portfolioRepository,
    InMemoryHoldingRepository holdingRepository,
    InMemoryPlayerStockRepository playerStockRepository,
    InMemoryTrackedPlayerRepository trackedPlayerRepository)
    : IPortfolioReadRepository
{
    public async Task<IReadOnlyList<PortfolioHoldingSummaryReadModel>> GetPortfolioSummaryHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await portfolioRepository.GetByUserIdAsync(userId, cancellationToken);
        if (portfolio is null)
        {
            return [];
        }

        var holdings = await holdingRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
        var items = new List<PortfolioHoldingSummaryReadModel>(holdings.Count);

        foreach (var holding in holdings)
        {
            var stock = await playerStockRepository.GetByIdAsync(holding.StockId, cancellationToken);
            var currentPrice = stock?.CurrentPrice ?? 0m;
            var playerName = stock is null
                ? null
                : (await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken))?.Username;
            var costBasis = holding.AveragePrice * holding.Quantity;
            var currentValue = currentPrice * holding.Quantity;

            items.Add(new PortfolioHoldingSummaryReadModel(
                holding.Id,
                holding.StockId,
                playerName,
                holding.Quantity,
                holding.AveragePrice,
                currentPrice,
                costBasis,
                currentValue,
                currentValue - costBasis));
        }

        return items;
    }

    public async Task<IReadOnlyList<HoldingReadModel>> GetHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await portfolioRepository.GetByUserIdAsync(userId, cancellationToken);
        if (portfolio is null)
        {
            return [];
        }

        var holdings = await holdingRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
        var items = new List<HoldingReadModel>(holdings.Count);

        foreach (var holding in holdings)
        {
            var stock = await playerStockRepository.GetByIdAsync(holding.StockId, cancellationToken);
            var currentPrice = stock?.CurrentPrice ?? 0m;
            var playerName = stock is null
                ? null
                : (await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken))?.Username;

            items.Add(new HoldingReadModel(
                holding.Id,
                holding.StockId,
                playerName,
                holding.Quantity,
                holding.AveragePrice,
                currentPrice));
        }

        return items;
    }
}
