using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Missions.GetMissions;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class MissionEndpoints
{
    public static void MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missionsGroup = app.MapGroup("/api/v1/missions")
            .RequireAuthorization()
            .WithTags("Missions");

        missionsGroup.MapGet("", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetMissionsQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                items = result.Value.Items
            });
        });
    }
}
