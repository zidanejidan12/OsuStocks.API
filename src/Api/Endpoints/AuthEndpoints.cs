using System.Security.Claims;
using MediatR;
using OsuStocks.Api.Common;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetCurrentUserProfile;
using OsuStocks.Application.Features.OsuIntegration.Auth.GetOsuLoginUrl;
using OsuStocks.Application.Features.OsuIntegration.Auth.HandleOsuCallback;
using static OsuStocks.Api.Common.EndpointAuth;

namespace OsuStocks.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/v1/auth")
            .RequireRateLimiting("auth")
            .WithTags("Auth");

        authGroup.MapGet("/login", async (
            string? returnUrl,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOsuLoginUrlQuery(returnUrl), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Redirect(result.Value.AuthorizationUrl);
        });

        authGroup.MapGet("/callback", async (
            string code,
            string state,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new HandleOsuCallbackCommand(code, state), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            var callback = result.Value;

            // The osu! handshake is a top-level browser navigation, so redirect the browser back to the
            // SPA callback page with the token in the URL fragment (kept out of server logs/history) rather
            // than returning JSON the frontend cannot consume from a full-page navigation.
            if (!string.IsNullOrWhiteSpace(callback.ReturnUrl))
            {
                var fragment =
                    $"accessToken={Uri.EscapeDataString(callback.AccessToken)}" +
                    $"&expiresAt={Uri.EscapeDataString(callback.ExpiresAt.ToString("o"))}";

                return Results.Redirect($"{callback.ReturnUrl}#{fragment}");
            }

            return Results.Ok(new
            {
                accessToken = callback.AccessToken,
                expiresAt = callback.ExpiresAt,
                returnUrl = callback.ReturnUrl
            });
        });

        authGroup.MapGet("/me", async (
            ClaimsPrincipal principal,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserId(principal, out var userId))
            {
                return Results.Json(new
                {
                    code = "UNAUTHORIZED",
                    message = "Authentication token is missing a valid user identifier.",
                    traceId = httpContext.TraceIdentifier
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await sender.Send(new GetCurrentUserProfileQuery(userId), cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                return result.Error!.ToErrorResult(httpContext);
            }

            return Results.Ok(new
            {
                userId = result.Value.UserId,
                osuUserId = result.Value.OsuUserId,
                username = result.Value.Username,
                avatarUrl = result.Value.AvatarUrl,
                countryCode = result.Value.CountryCode,
                role = result.Value.Role,
                investorLevel = result.Value.InvestorLevel
            });
        })
        .RequireAuthorization();
    }
}
