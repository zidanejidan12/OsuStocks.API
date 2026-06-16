using MediatR;
using OsuStocks.Application.Common.Caching;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketStocks;

public sealed class GetMarketStocksQueryHandler(
    IMarketReadRepository marketReadRepository,
    IReadModelCache readModelCache)
    : IRequestHandler<GetMarketStocksQuery, Result<GetMarketStocksResponse>>
{
    // Short TTL: the board changes with trades and sync cycles, so keep it fresh. This mainly
    // absorbs bursts of identical concurrent requests rather than caching for long.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    public async Task<Result<GetMarketStocksResponse>> Handle(
        GetMarketStocksQuery request,
        CancellationToken cancellationToken)
    {
        // Normalize sort/search so equivalent queries share a cache entry (matches repo behavior).
        var sortKey = string.IsNullOrWhiteSpace(request.Sort) ? "default" : request.Sort.Trim().ToLowerInvariant();
        var searchKey = string.IsNullOrWhiteSpace(request.Search) ? string.Empty : request.Search.Trim().ToLowerInvariant();
        var country = NormalizeCountry(request.Country);
        var countryKey = country ?? "all";
        var cacheKey = $"market-stocks:p{request.Page}:s{request.PageSize}:sort{sortKey}:q{searchKey}:c{countryKey}";

        var response = await readModelCache.GetOrSetAsync(
            cacheKey,
            CacheTtl,
            async ct =>
            {
                var result = await marketReadRepository.GetStocksAsync(
                    new MarketStocksQuerySpec(request.Page, request.PageSize, request.Sort, request.Search, country),
                    ct);

                return new GetMarketStocksResponse(
                    result.Items.Select(x => new MarketStockListItemResponse(
                        x.StockId,
                        x.PlayerName,
                        x.AvatarUrl,
                        x.CountryCode,
                        x.CurrentPrice,
                        x.Volume,
                        x.PriceChange24h)).ToList(),
                    request.Page,
                    request.PageSize,
                    result.TotalCount);
            },
            cancellationToken);

        return Result.Success(response);
    }

    // Trim + uppercase so equivalent country filters share a cache entry and the repo gets a canonical
    // ISO code. Null/empty/"ALL" mean "no country filter".
    private static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        var normalized = country.Trim().ToUpperInvariant();
        return normalized == "ALL" ? null : normalized;
    }
}
