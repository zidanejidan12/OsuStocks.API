using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.GetTradeHistory;

public sealed class GetTradeHistoryQueryHandler(
    ITradeRepository tradeRepository,
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository)
    : IRequestHandler<GetTradeHistoryQuery, Result<GetTradeHistoryResponse>>
{
    public async Task<Result<GetTradeHistoryResponse>> Handle(GetTradeHistoryQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;
        var trades = await tradeRepository.GetByUserIdAsync(request.UserId, skip, request.PageSize, cancellationToken);

        var items = new List<TradeHistoryItemResponse>(trades.Count);

        foreach (var trade in trades)
        {
            string? playerName = null;
            var stock = await playerStockRepository.GetByIdAsync(trade.StockId, cancellationToken);
            if (stock is not null)
            {
                var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken);
                playerName = trackedPlayer?.Username;
            }

            items.Add(new TradeHistoryItemResponse(
                trade.Id,
                trade.StockId,
                trade.TradeType.ToString(),
                trade.Quantity,
                trade.UnitPrice,
                trade.TotalAmount,
                trade.ExecutedAt,
                playerName));
        }

        return Result.Success(new GetTradeHistoryResponse(items));
    }
}
