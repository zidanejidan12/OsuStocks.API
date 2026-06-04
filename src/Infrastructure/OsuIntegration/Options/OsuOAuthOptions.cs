namespace OsuStocks.Infrastructure.OsuIntegration.Options;

public sealed class OsuOAuthOptions
{
    public const string SectionName = "OsuOAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://osu.ppy.sh/oauth/authorize";
    public string TokenEndpoint { get; set; } = "https://osu.ppy.sh/oauth/token";
    public string[] Scopes { get; set; } = ["public", "identify"];
}
