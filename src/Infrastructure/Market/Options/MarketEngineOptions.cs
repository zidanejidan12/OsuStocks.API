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

    // Hard ceiling on how much a single trade can move a price (both directions). Bounds the
    // pump-and-dump vector: a large order moves the price at most this much regardless of quantity.
    // NOT scaled by the admin trade multiplier — it's a safety cap, not a tunable sensitivity.
    public decimal MaxTradeImpact { get; set; } = 0.10m;

    // Progressive (PPh-21-style) trade fee charged on both buys and sells as an inflation sink — the
    // fee is burned (removed from circulation). Each bracket's Rate applies only to the portion of the
    // trade value within it (marginal); the top bracket is unbounded. The overall magnitude is scaled
    // live by MarketSettings.TradeFeeMultiplier. Order by UpTo ascending.
    public List<TradeFeeBracketOption> TradeFeeBrackets { get; set; } =
    [
        new() { UpTo = 10_000m, Rate = 0.005m },      // first 10k: 0.5%
        new() { UpTo = 100_000m, Rate = 0.01m },      // 10k–100k: 1%
        new() { UpTo = 1_000_000m, Rate = 0.02m },    // 100k–1M: 2%
        new() { UpTo = 1_000_000_000m, Rate = 0.03m } // above 1M: 3% (top bracket, treated as unbounded)
    ];
}

public sealed class TradeFeeBracketOption
{
    public decimal UpTo { get; set; }
    public decimal Rate { get; set; }
}
