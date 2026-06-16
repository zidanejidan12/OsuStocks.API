namespace OsuStocks.Infrastructure.Market.Options;

public sealed class MarketEngineOptions
{
    public const string SectionName = "MarketEngine";

    public decimal TradeBuyImpactPerShare { get; set; } = 0.0025m;
    public decimal TradeSellImpactPerShare { get; set; } = 0.0025m;
    // A new top play's price bump scales by playPp / playerPp: impact = clamp(scale * ratio, min, max).
    // Tuned so a typical elite play (~5% of the player's pp) lands near the old flat 3%, while breakout
    // plays (a large fraction of a smaller player's pp) move the stock more, up to the cap.
    public decimal TopPlayImpactScale { get; set; } = 0.6m;
    public decimal MaxTopPlayImpact { get; set; } = 0.10m;
    public decimal MinTopPlayImpact { get; set; } = 0.005m;
    public decimal PpImpactPerPoint { get; set; } = 0.0002m;
    public decimal MaxPpImpact { get; set; } = 0.10m;
    public decimal InactivityDecayImpact { get; set; } = 0.005m;
    public int InactivityThresholdDays { get; set; } = 7;
    public decimal PriceFloor { get; set; } = 1m;

    // Rank change is bidirectional: impact = clamp(scale * relativeRankMove, -max, +max).
    // relativeRankMove = (previousRank - currentRank) / previousRank.
    public decimal RankChangeImpactScale { get; set; } = 0.5m;
    public decimal MaxRankChangeImpact { get; set; } = 0.05m;
}
