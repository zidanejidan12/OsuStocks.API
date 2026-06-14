using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Achievements.GetAchievements;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class AchievementEndpoints
{
    public static void MapAchievementEndpoints(this IEndpointRouteBuilder app)
    {
        var achievementsGroup = app.MapGroup("/api/v1/achievements")
            .RequireAuthorization()
            .WithTags("Achievements");

        achievementsGroup.MapGet("", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetAchievementsQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                unlockedCount = result.Value.UnlockedCount,
                totalCount = result.Value.TotalCount,
                items = result.Value.Items
            });
        });
    }
}
