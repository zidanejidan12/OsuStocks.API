using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetLiveMovers;

public sealed class GetLiveMoversQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetLiveMoversQuery, Result<GetLiveMoversResponse>>
{
    public async Task<Result<GetLiveMoversResponse>> Handle(
        GetLiveMoversQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var movers = await marketReadRepository.GetTopMoversAsync(limit, cancellationToken);

        return Result.Success(new GetLiveMoversResponse(
            movers.Select(x => new LiveMoverResponse(
                x.StockId,
                x.PlayerName,
                x.AvatarUrl,
                x.CurrentPrice,
                x.PriceChange24h)).ToList()));
    }
}
