namespace OsuStocks.Domain.Market.Services;

using OsuStocks.Domain.Market.Models;

/// <summary>
/// Computes a progressive, PPh-21-style trade fee: each bracket's rate applies only to the portion of
/// the trade value within that bracket (marginal), so small trades pay a low effective rate and large
/// trades pay more. The result is scaled by a live <paramref name="multiplier"/> (admin-tunable) and
/// rounded to 2 decimals. The highest bracket is treated as unbounded.
/// </summary>
public static class TradeFeeCalculator
{
    public static decimal Compute(
        decimal tradeValue,
        IReadOnlyList<TradeFeeBracket> brackets,
        decimal multiplier)
    {
        if (tradeValue <= 0m || multiplier <= 0m || brackets is null || brackets.Count == 0)
        {
            return 0m;
        }

        var ordered = brackets.OrderBy(static b => b.UpTo).ToList();

        decimal fee = 0m;
        decimal lower = 0m;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (tradeValue <= lower)
            {
                break;
            }

            var isLast = i == ordered.Count - 1;
            // The top bracket is unbounded — it covers everything above the previous bound.
            var upper = isLast ? tradeValue : ordered[i].UpTo;

            var portion = Math.Min(tradeValue, upper) - lower;
            if (portion > 0m)
            {
                fee += portion * ordered[i].Rate;
            }

            lower = ordered[i].UpTo;
        }

        return Math.Round(fee * multiplier, 2, MidpointRounding.AwayFromZero);
    }
}
