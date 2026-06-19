namespace OsuStocks.Domain.Market.Services;

/// <summary>
/// Rank-based opening price for a newly tracked player's stock. A power-law curve over global rank
/// so stronger players list higher: <c>price = TopPrice * rank^(-Decay)</c>, floored at MinPrice.
/// Tuned so rank 1 ≈ 1000 and rank 500 ≈ 100. Unranked players (no global rank) fall back to a
/// neutral baseline. Shared by manual add (AddTrackedPlayer) and bulk seeding so both price identically.
/// </summary>
public static class InitialStockPriceCalculator
{
    private const decimal TopPrice = 1000m;
    private const double RankDecay = 0.37;
    private const decimal MinPrice = 1m;
    private const decimal UnrankedPrice = 100m;

    public static decimal Compute(int? globalRank)
    {
        if (globalRank is null or <= 0)
        {
            return UnrankedPrice;
        }

        var price = (decimal)((double)TopPrice * Math.Pow(globalRank.Value, -RankDecay));
        return Math.Round(Math.Max(price, MinPrice), 2);
    }
}
