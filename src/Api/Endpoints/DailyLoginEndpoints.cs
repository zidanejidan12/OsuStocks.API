using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.DailyLogin.ClaimDailyLoginReward;
using OsuStocks.Application.Features.DailyLogin.GetDailyLoginStatus;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class DailyLoginEndpoints
{
    public static void MapDailyLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var dailyLoginGroup = app.MapGroup("/api/v1/daily-login")
            .RequireAuthorization()
            .WithTags("DailyLogin");

        dailyLoginGroup.MapGet("", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetDailyLoginStatusQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                streak = result.Value.Streak,
                claimedToday = result.Value.ClaimedToday,
                todayAmount = result.Value.TodayAmount,
                schedule = result.Value.Schedule,
                serverTimeUtc = result.Value.ServerTimeUtc,
                nextResetUtc = result.Value.NextResetUtc
            });
        });

        dailyLoginGroup.MapPost("/claim", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new ClaimDailyLoginRewardCommand(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                granted = result.Value.Granted,
                alreadyClaimed = result.Value.AlreadyClaimed,
                amount = result.Value.Amount,
                streakDay = result.Value.StreakDay,
                newBalance = result.Value.NewBalance
            });
        });
    }
}
