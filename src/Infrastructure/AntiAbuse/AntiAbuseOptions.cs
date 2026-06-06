namespace OsuStocks.Infrastructure.AntiAbuse;

public sealed class AntiAbuseOptions
{
    public const string SectionName = "AntiAbuse";

    public decimal MaxOwnershipPercentage { get; set; } = 25m;
    public int TradeCooldownSeconds { get; set; } = 30;
    public int RapidTradeWindowSeconds { get; set; } = 300;
    public int RapidTradeThreshold { get; set; } = 10;
}
