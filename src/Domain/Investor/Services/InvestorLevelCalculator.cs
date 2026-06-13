using OsuStocks.Domain.Investor.Interfaces;
using OsuStocks.Domain.Investor.Models;

namespace OsuStocks.Domain.Investor.Services;

/// <summary>
/// Investor level curve modelled on the osu! score-to-level formula. Each level requires more
/// XP than the last (strictly increasing), with a soft cap at level 100: every level beyond 100
/// costs a flat <see cref="SoftCapXpPerLevel"/> (100 billion), making 100 -> 101 a very large jump.
///
/// For 1 &lt;= L &lt;= 100:  floor(L) = 5000/3 * (4L^3 - 3L^2 - L) + 1.25 * 1.8^(L-60)
/// For L &gt;= 100:        floor(L) = floor(100) + 100,000,000,000 * (L - 100)
///
/// The formula is pure and deterministic; thresholds for levels 1..100 are precomputed once.
/// </summary>
public sealed class InvestorLevelCalculator : IInvestorLevelCalculator
{
    public const int SoftCapLevel = 100;
    public const long SoftCapXpPerLevel = 100_000_000_000L;

    // Cumulative XP required to reach each level. Index = level (1..SoftCapLevel); index 0 unused.
    private static readonly long[] Thresholds = BuildThresholds();

    public long XpToReachLevel(int level)
    {
        if (level <= 1)
        {
            return 0L;
        }

        if (level <= SoftCapLevel)
        {
            return Thresholds[level];
        }

        return Thresholds[SoftCapLevel] + (SoftCapXpPerLevel * (level - SoftCapLevel));
    }

    public string GetTitle(int level) => level switch
    {
        >= 100 => "Market Legend",
        >= 75 => "Blue-Chip Trader",
        >= 50 => "Seasoned Investor",
        >= 25 => "Active Trader",
        >= 10 => "Retail Trader",
        _ => "Novice Investor",
    };

    public InvestorLevelProgress GetProgress(long totalXp)
    {
        if (totalXp < 0L)
        {
            totalXp = 0L;
        }

        var level = ResolveLevel(totalXp);

        var currentFloor = XpToReachLevel(level);
        var nextFloor = XpToReachLevel(level + 1);

        var xpIntoLevel = totalXp - currentFloor;
        var xpForNextLevel = nextFloor - currentFloor;
        var progressToNext = xpForNextLevel > 0L
            ? (double)xpIntoLevel / xpForNextLevel
            : 0d;

        return new InvestorLevelProgress(
            level,
            GetTitle(level),
            totalXp,
            xpIntoLevel,
            xpForNextLevel,
            progressToNext);
    }

    private static int ResolveLevel(long totalXp)
    {
        // Soft-capped region beyond level 100 is a flat linear cost per level.
        if (totalXp >= Thresholds[SoftCapLevel])
        {
            var extra = (totalXp - Thresholds[SoftCapLevel]) / SoftCapXpPerLevel;
            return SoftCapLevel + (int)extra;
        }

        // Largest level L in [1, 100) whose floor is <= totalXp. Thresholds are strictly ascending.
        var low = 1;
        var high = SoftCapLevel; // exclusive upper bound is handled by the branch above
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (Thresholds[mid] <= totalXp)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    private static long[] BuildThresholds()
    {
        var thresholds = new long[SoftCapLevel + 1];
        thresholds[0] = 0L;
        thresholds[1] = 0L;

        for (var level = 2; level <= SoftCapLevel; level++)
        {
            thresholds[level] = ScoreToReachLevel(level);
        }

        return thresholds;
    }

    private static long ScoreToReachLevel(int level)
    {
        double l = level;
        var polynomial = 5000d / 3d * ((4d * l * l * l) - (3d * l * l) - l);
        var exponential = 1.25d * Math.Pow(1.8d, l - 60d);
        return (long)Math.Round(polynomial + exponential, MidpointRounding.AwayFromZero);
    }
}
