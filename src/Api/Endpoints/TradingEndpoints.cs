using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Trading.BuyStock;
using OsuStocks.Application.Features.Trading.GetTradeHistory;
using OsuStocks.Application.Features.Trading.SellStock;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class TradingEndpoints
{
    public static void MapTradingEndpoints(this IEndpointRouteBuilder app)
    {
        var tradingGroup = app.MapGroup("/api/v1/trading")
            .RequireAuthorization()
            .RequireRateLimiting("trading")
            .WithTags("Trading");

        tradingGroup.MapPost("/buy", async (
            TradeStockRequest request,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new BuyStockCommand(userId, request.StockId, request.Quantity), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                tradeId = result.Value.TradeId,
                unitPrice = result.Value.UnitPrice,
                totalAmount = result.Value.TotalAmount,
                fee = result.Value.Fee,
                totalCost = result.Value.TotalAmount + result.Value.Fee,
                status = "Completed"
            });
        });

        tradingGroup.MapPost("/sell", async (
            TradeStockRequest request,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new SellStockCommand(userId, request.StockId, request.Quantity), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                tradeId = result.Value.TradeId,
                unitPrice = result.Value.UnitPrice,
                totalAmount = result.Value.TotalAmount,
                fee = result.Value.Fee,
                netAmount = result.Value.TotalAmount - result.Value.Fee,
                status = "Completed"
            });
        });

        tradingGroup.MapGet("/history", async (
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetTradeHistoryQuery(userId, page ?? 1, pageSize ?? 25), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { items = result.Value.Items });
        });
    }
}

public sealed record TradeStockRequest(Guid StockId, decimal Quantity);
