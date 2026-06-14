using System.Security.Claims;

namespace OsuStocks.Api.Common;

/// <summary>
/// Shared helpers for endpoint modules: resolving the caller's identity from claims and the
/// standard unauthorized response. Imported via <c>using static</c> so endpoint handlers can call
/// <see cref="TryResolveUserId"/> / <see cref="UnauthorizedResult"/> / <see cref="ResolveActor"/>
/// unqualified.
/// </summary>
internal static class EndpointAuth
{
    public static bool TryResolveUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    public static IResult UnauthorizedResult(HttpContext httpContext)
    {
        return Results.Json(new
        {
            code = "UNAUTHORIZED",
            message = "Authentication token is missing a valid user identifier.",
            traceId = httpContext.TraceIdentifier
        }, statusCode: StatusCodes.Status401Unauthorized);
    }

    public static string? ResolveActor(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.Identity?.Name;
    }
}
