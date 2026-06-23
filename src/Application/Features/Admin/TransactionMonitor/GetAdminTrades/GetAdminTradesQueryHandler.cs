using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminTrades;

public sealed class GetAdminTradesQueryHandler(IAdminTransactionReadRepository repository)
    : IRequestHandler<GetAdminTradesQuery, Result<GetAdminTradesResponse>>
{
    public async Task<Result<GetAdminTradesResponse>> Handle(
        GetAdminTradesQuery request,
        CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var (trades, totalCount) = await repository.GetTradesAsync(
            request.UserId,
            request.StockId,
            request.TradeType,
            request.From,
            request.To,
            skip,
            request.PageSize,
            cancellationToken);

        var items = trades
            .Select(x => new AdminTradeItemResponse(
                x.TradeId,
                x.UserId,
                x.Username,
                x.AvatarUrl,
                x.StockId,
                x.PlayerName,
                x.TradeType.ToString(),
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                x.ExecutedAt))
            .ToList();

        return Result.Success(new GetAdminTradesResponse(items, totalCount, request.Page, request.PageSize));
    }
}
