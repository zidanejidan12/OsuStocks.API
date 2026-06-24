using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Market.GetLiveMovers;
using OsuStocks.Application.Features.Market.GetMarketCountries;
using OsuStocks.Application.Features.Market.GetMarketOverview;
using OsuStocks.Application.Features.Market.GetMarketStockDetails;
using OsuStocks.Application.Features.Market.GetMarketStockHistory;
using OsuStocks.Application.Features.Market.GetMarketStocks;
using OsuStocks.Application.Features.Market.GetStockAnalytics;
using OsuStocks.Application.Features.Market.GetStockCandles;
using OsuStocks.Application.Features.Market.GetStockTopPlays;
using OsuStocks.Application.Features.Market.GetTradeQuote;

namespace OsuStocks.Api.Endpoints;

internal static class MarketEndpoints
{
    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var marketGroup = app.MapGroup("/api/v1/market")
            .RequireAuthorization()
            .WithTags("Market");

        // Public: powers the logged-out landing-page live ticker. Read-only, non-sensitive
        // (public osu! player names, prices, 24h change), so it opts out of the group's auth.
        marketGroup.MapGet("/movers", async (
            int? limit,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetLiveMoversQuery(limit ?? 8), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new
                {
                    stockId = x.StockId,
                    playerName = x.PlayerName,
                    avatarUrl = x.AvatarUrl,
                    currentPrice = x.CurrentPrice,
                    priceChange24h = x.PriceChange24h
                })
            });
        })
        .AllowAnonymous();

        marketGroup.MapGet("", async (
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMarketOverviewQuery(), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            Dictionary<string, object?> ToMoverObject(OsuStocks.Application.Features.Market.GetMarketOverview.MarketTopMoverResponse? mover)
            {
                if (mover is null)
                {
                    return new Dictionary<string, object?>();
                }

                return new Dictionary<string, object?>
                {
                    ["stockId"] = mover.StockId,
                    ["playerName"] = mover.PlayerName,
                    ["avatarUrl"] = mover.AvatarUrl,
                    ["currentPrice"] = mover.CurrentPrice,
                    ["priceChange24h"] = mover.PriceChange24h
                };
            }

            return Results.Ok(new
            {
                totalStocks = result.Value.TotalStocks,
                totalVolume = result.Value.TotalVolume,
                topGainer = ToMoverObject(result.Value.TopGainer),
                topLoser = ToMoverObject(result.Value.TopLoser)
            });
        });

        marketGroup.MapGet("/stocks", async (
            int? page,
            int? pageSize,
            string? sort,
            string? search,
            string? country,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMarketStocksQuery(
                page ?? 1,
                pageSize ?? 25,
                sort,
                search,
                country), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                page = result.Value.Page,
                pageSize = result.Value.PageSize,
                totalCount = result.Value.TotalCount
            });
        });

        marketGroup.MapGet("/countries", async (
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMarketCountriesQuery(), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new
                {
                    countryCode = x.CountryCode,
                    count = x.Count
                })
            });
        });

        marketGroup.MapGet("/stocks/{stockId:guid}", async (
            Guid stockId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMarketStockDetailsQuery(stockId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                stockId = result.Value.StockId,
                playerName = result.Value.PlayerName,
                avatarUrl = result.Value.AvatarUrl,
                countryCode = result.Value.CountryCode,
                currentPrice = result.Value.CurrentPrice,
                volume = result.Value.Volume,
                priceChange24h = result.Value.PriceChange24h,
                globalRank = result.Value.GlobalRank,
                currentPp = result.Value.CurrentPp,
                bannerUrl = result.Value.BannerUrl
            });
        });

        marketGroup.MapGet("/stocks/{stockId:guid}/history", async (
            Guid stockId,
            string? range,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(range))
            {
                var candlesResult = await sender.Send(new GetStockCandlesQuery(stockId, range), cancellationToken);
                if (!candlesResult.IsSuccess || candlesResult.Value is null)
                {
                    return candlesResult.Error!.ToErrorResult(httpContext);
                }

                return Results.Ok(new
                {
                    range = candlesResult.Value.Range,
                    candles = candlesResult.Value.Items.Select(x => new
                    {
                        bucketStart = x.BucketStart,
                        open = x.Open,
                        high = x.High,
                        low = x.Low,
                        close = x.Close,
                        volume = x.Volume
                    })
                });
            }

            var result = await sender.Send(new GetMarketStockHistoryQuery(stockId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(result.Value.Items.Select(x => new
            {
                timestamp = x.Timestamp,
                price = x.Price
            }));
        });

        marketGroup.MapGet("/stocks/{stockId:guid}/top-plays", async (
            Guid stockId,
            int? limit,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetStockTopPlaysQuery(stockId, limit ?? 5), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new
                {
                    scoreId = x.ScoreId,
                    pp = x.Pp,
                    coverUrl = x.CoverUrl,
                    title = x.Title,
                    percentChange = x.PercentChange,
                    newPrice = x.NewPrice,
                    occurredAt = x.OccurredAt
                })
            });
        });

        marketGroup.MapGet("/stocks/{stockId:guid}/analytics", async (
            Guid stockId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetStockAnalyticsQuery(stockId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                volume24hShares = result.Value.Volume24hShares,
                volume24hValue = result.Value.Volume24hValue,
                volume7dShares = result.Value.Volume7dShares,
                volume7dValue = result.Value.Volume7dValue,
                volatility7d = result.Value.Volatility7d,
                ownershipCount = result.Value.OwnershipCount,
                activeTraders24h = result.Value.ActiveTraders24h,
                marketCap = result.Value.MarketCap,
                liquidity = result.Value.Liquidity,
                liquidityTier = result.Value.LiquidityTier,
                totalShares = result.Value.TotalShares,
                maxOwnershipPercentage = result.Value.MaxOwnershipPercentage,
                referenceSupplyShares = result.Value.ReferenceSupplyShares
            });
        });

        // Pre-trade estimate: exact fill (slippage + spread) + progressive fee for a given quantity.
        marketGroup.MapGet("/stocks/{stockId:guid}/quote", async (
            Guid stockId,
            decimal quantity,
            string? side,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var isSell = string.Equals(side, "sell", StringComparison.OrdinalIgnoreCase);
            var result = await sender.Send(new GetTradeQuoteQuery(stockId, quantity, isSell), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                quantity = result.Value.Quantity,
                unitPrice = result.Value.UnitPrice,
                grossAmount = result.Value.GrossAmount,
                fee = result.Value.Fee,
                total = result.Value.Total,
                newPrice = result.Value.NewPrice,
                isSell = result.Value.IsSell
            });
        });

        marketGroup.MapGet("/events", async (
            int? page,
            int? pageSize,
            string? type,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Market.GetMarketActivityFeed.GetMarketActivityFeedQuery(
                page ?? 1,
                pageSize ?? 25,
                type), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new
                {
                    stockId = x.StockId,
                    playerName = x.PlayerName,
                    avatarUrl = x.AvatarUrl,
                    countryCode = x.CountryCode,
                    reason = x.Reason,
                    description = x.Description,
                    percentChange = x.PercentChange,
                    newPrice = x.NewPrice,
                    occurredAt = x.OccurredAt
                }),
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });

        marketGroup.MapGet("/events/{stockId:guid}", async (
            Guid stockId,
            int? page,
            int? pageSize,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Market.GetStockActivityFeed.GetStockActivityFeedQuery(
                stockId,
                page ?? 1,
                pageSize ?? 25), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items.Select(x => new
                {
                    stockId = x.StockId,
                    playerName = x.PlayerName,
                    avatarUrl = x.AvatarUrl,
                    countryCode = x.CountryCode,
                    reason = x.Reason,
                    description = x.Description,
                    percentChange = x.PercentChange,
                    newPrice = x.NewPrice,
                    occurredAt = x.OccurredAt
                }),
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });

        marketGroup.MapGet("/trending", async (
            string? window,
            int? limit,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Market.GetTrending.GetTrendingQuery(
                window,
                limit ?? 10), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            object MapSection(IReadOnlyList<OsuStocks.Application.Features.Market.GetTrending.TrendingStockResponse> section) =>
                section.Select(x => new
                {
                    stockId = x.StockId,
                    playerName = x.PlayerName,
                    avatarUrl = x.AvatarUrl,
                    countryCode = x.CountryCode,
                    metricValue = x.MetricValue,
                    currentPrice = x.CurrentPrice
                });

            return Results.Ok(new
            {
                mostBought = MapSection(result.Value.MostBought),
                mostSold = MapSection(result.Value.MostSold),
                fastestRising = MapSection(result.Value.FastestRising),
                fastestFalling = MapSection(result.Value.FastestFalling),
                highestVolume = MapSection(result.Value.HighestVolume)
            });
        });
    }
}
