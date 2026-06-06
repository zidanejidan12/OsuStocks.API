using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Infrastructure.Security;

internal sealed class OAuthReturnUrlPolicy(IOptions<OAuthReturnUrlOptions> options, IHostEnvironment environment)
    : IOAuthReturnUrlPolicy
{
    private readonly HashSet<string> _allowedOrigins = BuildAllowedOrigins(options.Value.AllowedOrigins);

    public bool IsAllowed(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!IsHttpOrHttps(uri))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return environment.IsDevelopment();
        }

        return _allowedOrigins.Contains(GetOrigin(uri));
    }

    private static bool IsHttpOrHttps(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string GetOrigin(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static HashSet<string> BuildAllowedOrigins(IEnumerable<string> origins)
    {
        var normalizedOrigins = origins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out var parsed) && IsHttpOrHttps(parsed))
            .Select(origin =>
            {
                var parsed = new Uri(origin);
                return GetOrigin(parsed);
            });

        return new HashSet<string>(normalizedOrigins, StringComparer.OrdinalIgnoreCase);
    }
}
