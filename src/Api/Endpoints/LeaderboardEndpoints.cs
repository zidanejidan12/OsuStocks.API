using MediatR;
using OsuStocks.Api.Common;

namespace OsuStocks.Api.Endpoints;

internal static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var leaderboardGroup = app.MapGroup("/api/v1/leaderboards")
            .RequireAuthorization()
            .WithTags("Leaderboards");

        leaderboardGroup.MapGet("/wealth", async (
            string? period,
            int? page,
            int? pageSize,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Leaderboards.GetWealthLeaderboard.GetWealthLeaderboardQuery(
                period,
                page ?? 1,
                pageSize ?? 25), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                period = result.Value.Period,
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });

        leaderboardGroup.MapGet("/profit", async (
            string? period,
            int? page,
            int? pageSize,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Leaderboards.GetProfitLeaderboard.GetProfitLeaderboardQuery(
                period,
                page ?? 1,
                pageSize ?? 25), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                period = result.Value.Period,
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });

        leaderboardGroup.MapGet("/traders", async (
            string? period,
            int? page,
            int? pageSize,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new OsuStocks.Application.Features.Leaderboards.GetTraderLeaderboard.GetTraderLeaderboardQuery(
                period,
                page ?? 1,
                pageSize ?? 25), cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items,
                period = result.Value.Period,
                page = result.Value.Page,
                pageSize = result.Value.PageSize
            });
        });
    }
}
