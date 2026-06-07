using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetStockAnalytics;

public sealed class GetStockAnalyticsQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetStockAnalyticsQuery, Result<GetStockAnalyticsResponse>>
{
    public async Task<Result<GetStockAnalyticsResponse>> Handle(
        GetStockAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var analytics = await marketReadRepository.GetStockAnalyticsAsync(request.StockId, cancellationToken);
        if (analytics is null)
        {
            return Result.Failure<GetStockAnalyticsResponse>("NOT_FOUND", "Stock not found.");
        }

        return Result.Success(new GetStockAnalyticsResponse(
            analytics.Volume24hShares,
            analytics.Volume24hValue,
            analytics.Volume7dShares,
            analytics.Volume7dValue,
            analytics.Volatility7d,
            analytics.OwnershipCount,
            analytics.ActiveTraders24h,
            analytics.MarketCap));
    }
}
