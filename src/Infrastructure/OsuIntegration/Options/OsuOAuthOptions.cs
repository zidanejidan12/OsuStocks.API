namespace OsuStocks.Infrastructure.OsuIntegration.Options;

public sealed class OsuOAuthOptions
{
    public const string SectionName = "OsuOAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://osu.ppy.sh/oauth/authorize";
    public string TokenEndpoint { get; set; } = "https://osu.ppy.sh/oauth/token";
    // No in-code default: the .NET configuration binder APPENDS bound array items to a pre-populated
    // collection, so a default here would duplicate the appsettings values (e.g. "public identify
    // public identify"). All hosts (Api + Worker, base + Development) define OsuOAuth:Scopes in config.
    public string[] Scopes { get; set; } = [];
}
