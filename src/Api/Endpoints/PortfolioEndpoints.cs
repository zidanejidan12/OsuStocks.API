using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;
using OsuStocks.Application.Features.Trading.GetHoldings;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var portfolioGroup = app.MapGroup("/api/v1/portfolio")
            .RequireAuthorization()
            .WithTags("Portfolio");

        portfolioGroup.MapGet("", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetPortfolioSummaryQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                currentValue = result.Value.CurrentValue,
                costBasis = result.Value.CostBasis,
                profitLoss = result.Value.ProfitLoss,
                holdings = result.Value.Holdings
            });
        });

        portfolioGroup.MapGet("/holdings", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetHoldingsQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new { items = result.Value.Items });
        });
    }
}
