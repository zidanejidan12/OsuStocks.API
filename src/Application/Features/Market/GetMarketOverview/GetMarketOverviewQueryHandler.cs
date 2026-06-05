using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketOverview;

public sealed class GetMarketOverviewQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetMarketOverviewQuery, Result<GetMarketOverviewResponse>>
{
    public async Task<Result<GetMarketOverviewResponse>> Handle(
        GetMarketOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var overview = await marketReadRepository.GetOverviewAsync(cancellationToken);

        return Result.Success(new GetMarketOverviewResponse(
            overview.TotalStocks,
            overview.TotalVolume,
            overview.TopGainer is null
                ? null
                : new MarketTopMoverResponse(
                    overview.TopGainer.StockId,
                    overview.TopGainer.PlayerName,
                    overview.TopGainer.CurrentPrice,
                    overview.TopGainer.PriceChange24h),
            overview.TopLoser is null
                ? null
                : new MarketTopMoverResponse(
                    overview.TopLoser.StockId,
                    overview.TopLoser.PlayerName,
                    overview.TopLoser.CurrentPrice,
                    overview.TopLoser.PriceChange24h)));
    }
}
