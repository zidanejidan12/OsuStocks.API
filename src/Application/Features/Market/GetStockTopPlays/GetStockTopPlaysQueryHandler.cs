using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetStockTopPlays;

public sealed class GetStockTopPlaysQueryHandler(IMarketActivityReadRepository marketActivityReadRepository)
    : IRequestHandler<GetStockTopPlaysQuery, Result<GetStockTopPlaysResponse>>
{
    public async Task<Result<GetStockTopPlaysResponse>> Handle(
        GetStockTopPlaysQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);

        var items = await marketActivityReadRepository.GetTopPlaysByStockAsync(
            request.StockId,
            skip: 0,
            take: limit,
            cancellationToken);

        return Result.Success(new GetStockTopPlaysResponse(
            items.Select(x => new StockTopPlayItemResponse(
                x.ScoreId,
                x.Pp,
                x.CoverUrl,
                x.Title,
                x.PercentChange,
                x.NewPrice,
                x.OccurredAt)).ToList()));
    }
}
