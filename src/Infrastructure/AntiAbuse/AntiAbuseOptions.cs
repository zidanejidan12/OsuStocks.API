namespace OsuStocks.Infrastructure.AntiAbuse;

public sealed class AntiAbuseOptions
{
    public const string SectionName = "AntiAbuse";

    public decimal MaxOwnershipPercentage { get; set; } = 25m;

    // Virtual "reference supply" added to a stock's real float when enforcing the ownership
    // cap. On a thin/new stock the real float is tiny, so MaxOwnershipPercentage of it is near
    // zero — which lets the first buyer lock everyone else out (the gatekeeping exploit). Pricing
    // the cap against (float + ReferenceSupplyShares) gives every trader a meaningful allowance
    // while the float is small, then tapers to the true percentage as the float grows past it.
    public decimal ReferenceSupplyShares { get; set; } = 100m;

    public int TradeCooldownSeconds { get; set; } = 30;
    public int RapidTradeWindowSeconds { get; set; } = 300;
    public int RapidTradeThreshold { get; set; } = 10;
}
