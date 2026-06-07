using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketActivityFeed;

public sealed class GetMarketActivityFeedQueryHandler(IMarketActivityReadRepository marketActivityReadRepository)
    : IRequestHandler<GetMarketActivityFeedQuery, Result<GetMarketActivityFeedResponse>>
{
    public async Task<Result<GetMarketActivityFeedResponse>> Handle(
        GetMarketActivityFeedQuery request,
        CancellationToken cancellationToken)
    {
        var items = await marketActivityReadRepository.GetFeedAsync(
            (request.Page - 1) * request.PageSize,
            request.PageSize,
            request.Reason,
            cancellationToken);

        return Result.Success(new GetMarketActivityFeedResponse(
            items.Select(x => new MarketActivityItemResponse(
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
