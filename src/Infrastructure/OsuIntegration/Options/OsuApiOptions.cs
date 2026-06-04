namespace OsuStocks.Infrastructure.OsuIntegration.Options;

public sealed class OsuApiOptions
{
    public const string SectionName = "OsuApi";

    public string BaseUrl { get; set; } = "https://osu.ppy.sh/api/v2/";
}
