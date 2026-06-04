namespace OsuStocks.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "osu-stocks";
    public string Audience { get; set; } = "osu-stocks-client";
    public string SigningKey { get; set; } = "replace-with-at-least-32-characters-signing-key";
    public int ExpirationMinutes { get; set; } = 60;
}
