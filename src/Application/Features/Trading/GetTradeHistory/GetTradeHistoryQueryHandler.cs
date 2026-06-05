using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.GetTradeHistory;

public sealed class GetTradeHistoryQueryHandler(ITradeReadRepository tradeReadRepository)
    : IRequestHandler<GetTradeHistoryQuery, Result<GetTradeHistoryResponse>>
{
    public async Task<Result<GetTradeHistoryResponse>> Handle(GetTradeHistoryQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;
        var trades = await tradeReadRepository.GetTradeHistoryByUserIdAsync(
            request.UserId,
            skip,
            request.PageSize,
            cancellationToken);

        var items = trades
            .Select(x => new TradeHistoryItemResponse(
                x.TradeId,
                x.StockId,
                x.TradeType.ToString(),
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.ExecutedAt,
                x.PlayerName))
            .ToList();

        return Result.Success(new GetTradeHistoryResponse(items));
    }
}
