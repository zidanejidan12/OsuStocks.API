using Hangfire.Dashboard;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Api.Security;

internal sealed class HangfireDashboardAuthorizationFilter(IHostEnvironment environment)
    : IDashboardAuthorizationFilter
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

        return environment.IsDevelopment() || httpContext.Request.IsHttps;
    }
}
