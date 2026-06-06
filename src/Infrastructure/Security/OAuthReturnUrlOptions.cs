namespace OsuStocks.Infrastructure.Security;

public sealed class OAuthReturnUrlOptions
{
    public const string SectionName = "Security:OAuthReturnUrl";

    public string[] AllowedOrigins { get; set; } = [];
}
