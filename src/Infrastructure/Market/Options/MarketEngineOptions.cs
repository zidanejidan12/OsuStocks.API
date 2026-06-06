namespace OsuStocks.Infrastructure.Market.Options;

public sealed class MarketEngineOptions
{
    public const string SectionName = "MarketEngine";

    public decimal TradeBuyImpactPerShare { get; set; } = 0.0025m;
    public decimal TradeSellImpactPerShare { get; set; } = 0.0025m;
    public decimal TopPlayImpact { get; set; } = 0.03m;
    public decimal PpImpactPerPoint { get; set; } = 0.0002m;
    public decimal MaxPpImpact { get; set; } = 0.10m;
    public decimal InactivityDecayImpact { get; set; } = 0.005m;
    public int InactivityThresholdDays { get; set; } = 7;
    public decimal PriceFloor { get; set; } = 1m;
}
