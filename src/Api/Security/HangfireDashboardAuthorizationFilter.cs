using Hangfire.Dashboard;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Api.Security;

internal sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return false;
        }

        if (!user.IsInRole(UserRole.Admin.ToString()))
        {
            return false;
        }

        // Require HTTPS unconditionally. Plaintext is permitted only for loopback connections
        // (local development / on-box access), never for remote clients over HTTP.
        return httpContext.Request.IsHttps || IsLoopback(httpContext);
    }

    private static bool IsLoopback(HttpContext httpContext)
    {
        var connection = httpContext.Connection;
        var remoteIp = connection.RemoteIpAddress;

        // A missing remote address (e.g. in-memory test server) is treated as loopback.
        return remoteIp is null
            || System.Net.IPAddress.IsLoopback(remoteIp)
            || remoteIp.Equals(connection.LocalIpAddress);
    }
}
