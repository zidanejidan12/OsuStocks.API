namespace OsuStocks.Domain.Market.Services;

/// <summary>
/// A stock's liquidity = its float (shares held across all users) plus weighted recent trade volume.
/// Higher liquidity means orders move the price (and pay spread) less. A qualitative tier is derived
/// relative to the reference depth for display.
/// </summary>
public static class LiquidityCalculator
{
    public static decimal Liquidity(decimal sharesOutstanding, decimal recentVolumeShares, decimal volumeWeight)
        => Math.Max(0m, sharesOutstanding) + Math.Max(0m, volumeWeight) * Math.Max(0m, recentVolumeShares);

    public static string Tier(decimal liquidity, decimal referenceDepth)
    {
        if (referenceDepth <= 0m)
        {
            return "Moderate";
        }

        var ratio = liquidity / referenceDepth;
        return ratio >= 5m ? "Deep" : ratio >= 1m ? "Moderate" : "Thin";
    }
}
