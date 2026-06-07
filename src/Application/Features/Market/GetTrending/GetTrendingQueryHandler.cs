using MediatR;
using OsuStocks.Application.Common.Caching;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Models.Market;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetTrending;

public sealed class GetTrendingQueryHandler(
    ITrendingReadRepository trendingReadRepository,
    IReadModelCache readModelCache)
    : IRequestHandler<GetTrendingQuery, Result<GetTrendingResponse>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<Result<GetTrendingResponse>> Handle(
        GetTrendingQuery request,
        CancellationToken cancellationToken)
    {
        var window = string.IsNullOrWhiteSpace(request.Window)
            ? "24h"
            : request.Window.Trim().ToLowerInvariant();

        var windowStart = ResolveWindowStart(window);

        var response = await readModelCache.GetOrSetAsync(
            $"trending:{window}:{request.Limit}",
            CacheTtl,
            async ct =>
            {
                var model = await trendingReadRepository.GetTrendingAsync(windowStart, request.Limit, ct);
                return MapToResponse(model);
            },
            cancellationToken);

        return Result.Success(response);
    }

    private static DateTimeOffset ResolveWindowStart(string window)
    {
        var now = DateTimeOffset.UtcNow;

        return window switch
        {
            "1h" => now.AddHours(-1),
            "7d" => now.AddDays(-7),
            _ => now.AddHours(-24)
        };
    }

    private static GetTrendingResponse MapToResponse(TrendingReadModel model)
    {
        return new GetTrendingResponse(
            MapSection(model.MostBought),
            MapSection(model.MostSold),
            MapSection(model.FastestRising),
            MapSection(model.FastestFalling),
            MapSection(model.HighestVolume));
    }

    private static IReadOnlyList<TrendingStockResponse> MapSection(IReadOnlyList<TrendingStockReadModel> section)
    {
        return section
            .Select(x => new TrendingStockResponse(x.StockId, x.PlayerName, x.AvatarUrl, x.CountryCode, x.MetricValue, x.CurrentPrice))
            .ToList();
    }
}
