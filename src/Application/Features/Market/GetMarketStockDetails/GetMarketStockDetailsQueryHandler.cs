using MediatR;
using OsuStocks.Application.Common.Caching;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed class GetMarketStockDetailsQueryHandler(
    IMarketReadRepository marketReadRepository,
    IReadModelCache readModelCache)
    : IRequestHandler<GetMarketStockDetailsQuery, Result<GetMarketStockDetailsResponse>>
{
    // Short TTL: price/rank change with trades and sync cycles, so keep it fresh.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    public async Task<Result<GetMarketStockDetailsResponse>> Handle(
        GetMarketStockDetailsQuery request,
        CancellationToken cancellationToken)
    {
        // A null factory result is not effectively cached (the cache re-runs the factory on a
        // "null" payload), so missing stocks never get stuck as cached not-found.
        var response = await readModelCache.GetOrSetAsync<GetMarketStockDetailsResponse?>(
            $"stock-detail:{request.StockId}",
            CacheTtl,
            async ct =>
            {
                var stock = await marketReadRepository.GetStockDetailsAsync(request.StockId, ct);
                if (stock is null)
                {
                    return null;
                }

                return new GetMarketStockDetailsResponse(
                    stock.StockId,
                    stock.PlayerName,
                    stock.AvatarUrl,
                    stock.CountryCode,
                    stock.CurrentPrice,
                    stock.Volume,
                    stock.PriceChange24h,
                    stock.GlobalRank,
                    stock.CurrentPp,
                    stock.ProfileCoverUrl);
            },
            cancellationToken);

        if (response is null)
        {
            return Result.Failure<GetMarketStockDetailsResponse>("NOT_FOUND", "Stock not found.");
        }

        return Result.Success(response);
    }
}
