using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Investor.GetInvestorLevel;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class InvestorEndpoints
{
    public static void MapInvestorEndpoints(this IEndpointRouteBuilder app)
    {
        var investorGroup = app.MapGroup("/api/v1/investor")
            .RequireAuthorization()
            .WithTags("Investor");

        investorGroup.MapGet("/level", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(new GetInvestorLevelQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                level = result.Value.Level,
                title = result.Value.Title,
                totalXp = result.Value.TotalXp,
                xpIntoLevel = result.Value.XpIntoLevel,
                xpForNextLevel = result.Value.XpForNextLevel,
                progressToNext = result.Value.ProgressToNext
            });
        });
    }
}
