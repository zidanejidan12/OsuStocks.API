using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryTradeReadRepository(
    InMemoryTradeRepository tradeRepository,
    InMemoryPlayerStockRepository playerStockRepository,
    InMemoryTrackedPlayerRepository trackedPlayerRepository)
    : ITradeReadRepository
{
    public async Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryByUserIdAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var trades = await tradeRepository.GetByUserIdAsync(userId, skip, take, cancellationToken);
        var items = new List<TradeHistoryReadModel>(trades.Count);

        foreach (var trade in trades)
        {
            var stock = await playerStockRepository.GetByIdAsync(trade.StockId, cancellationToken);
            var playerName = stock is null
                ? null
                : (await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken))?.Username;

            items.Add(new TradeHistoryReadModel(
                trade.Id,
                trade.StockId,
                trade.TradeType,
                trade.Quantity,
                trade.UnitPrice,
                trade.TotalAmount,
                trade.ExecutedAt,
                playerName));
        }

        return items;
    }

    public Task<decimal> GetSharesTradedSinceAsync(
        Guid stockId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
        => tradeRepository.SumQuantityByStockSinceAsync(stockId, since, cancellationToken);
}
