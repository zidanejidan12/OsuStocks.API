using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketStockHistory;

public sealed class GetMarketStockHistoryQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetMarketStockHistoryQuery, Result<GetMarketStockHistoryResponse>>
{
    public async Task<Result<GetMarketStockHistoryResponse>> Handle(
        GetMarketStockHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await marketReadRepository.GetStockDetailsAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<GetMarketStockHistoryResponse>("NOT_FOUND", "Stock not found.");
        }

        var history = await marketReadRepository.GetStockHistoryAsync(request.StockId, cancellationToken);

        return Result.Success(new GetMarketStockHistoryResponse(
            history.Select(x => new MarketStockHistoryPointResponse(x.Timestamp, x.Price)).ToList()));
    }
}
