using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.Profile.UpdateProfileShowcase;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var profileGroup = app.MapGroup("/api/v1/profile")
            .RequireAuthorization()
            .WithTags("Profile");

        // Sets the player's equipped title + showcased achievements (validated server-side
        // against what they've actually unlocked). The read side is on GET /auth/me.
        profileGroup.MapPut("/showcase", async (
            UpdateProfileShowcaseRequest body,
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return UnauthorizedResult(httpContext);
            }

            var result = await sender.Send(
                new UpdateProfileShowcaseCommand(
                    userId,
                    body.EquippedTitleCode,
                    body.ShowcasedAchievementCodes ?? []),
                cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                equippedTitleCode = result.Value.EquippedTitleCode,
                equippedTitle = result.Value.EquippedTitle,
                showcasedAchievementCodes = result.Value.ShowcasedAchievementCodes
            });
        });
    }

    internal sealed record UpdateProfileShowcaseRequest(
        string? EquippedTitleCode,
        IReadOnlyList<string>? ShowcasedAchievementCodes);
}
