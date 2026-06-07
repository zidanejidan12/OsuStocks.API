using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetStockActivityFeed;

public sealed class GetStockActivityFeedQueryHandler(IMarketActivityReadRepository marketActivityReadRepository)
    : IRequestHandler<GetStockActivityFeedQuery, Result<GetStockActivityFeedResponse>>
{
    public async Task<Result<GetStockActivityFeedResponse>> Handle(
        GetStockActivityFeedQuery request,
        CancellationToken cancellationToken)
    {
        var items = await marketActivityReadRepository.GetFeedByStockAsync(
            request.StockId,
            (request.Page - 1) * request.PageSize,
            request.PageSize,
            cancellationToken);

        return Result.Success(new GetStockActivityFeedResponse(
            items.Select(x => new StockActivityItemResponse(
                x.StockId,
                x.PlayerName,
                x.AvatarUrl,
                x.CountryCode,
                x.Reason,
                x.Description,
                x.PercentChange,
                x.NewPrice,
                x.OccurredAt)).ToList(),
            request.Page,
            request.PageSize));
    }
}
