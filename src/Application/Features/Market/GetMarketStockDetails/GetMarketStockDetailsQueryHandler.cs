using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed class GetMarketStockDetailsQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetMarketStockDetailsQuery, Result<GetMarketStockDetailsResponse>>
{
    public async Task<Result<GetMarketStockDetailsResponse>> Handle(
        GetMarketStockDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await marketReadRepository.GetStockDetailsAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<GetMarketStockDetailsResponse>("NOT_FOUND", "Stock not found.");
        }

        return Result.Success(new GetMarketStockDetailsResponse(
            stock.StockId,
            stock.PlayerName,
            stock.AvatarUrl,
            stock.CountryCode,
            stock.CurrentPrice,
            stock.Volume,
            stock.PriceChange24h,
            stock.GlobalRank,
            stock.CurrentPp));
    }
}
