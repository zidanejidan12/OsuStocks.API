using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetStockCandles;

public sealed class GetStockCandlesQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetStockCandlesQuery, Result<GetStockCandlesResponse>>
{
    public async Task<Result<GetStockCandlesResponse>> Handle(
        GetStockCandlesQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await marketReadRepository.GetStockDetailsAsync(request.StockId, cancellationToken);
        if (stock is null)
        {
            return Result.Failure<GetStockCandlesResponse>("NOT_FOUND", "Stock not found.");
        }

        var spec = HistoryRangeSpec.FromRange(request.Range, DateTimeOffset.UtcNow);

        var candles = await marketReadRepository.GetStockCandlesAsync(request.StockId, spec, cancellationToken);

        return Result.Success(new GetStockCandlesResponse(
            spec.Range,
            candles
                .Select(x => new StockCandleResponse(
                    x.BucketStart, x.Open, x.High, x.Low, x.Close, x.Volume))
                .ToList()));
    }
}
