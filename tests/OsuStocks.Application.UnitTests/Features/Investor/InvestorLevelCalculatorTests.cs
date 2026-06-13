using OsuStocks.Domain.Investor.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Investor;

/// <summary>
/// Verifies the osu!-style investor level curve: level 1 starts at 0 XP, each level requires
/// strictly more XP than the previous one, and level 100 is a soft cap (each subsequent level
/// costs a flat 100 billion XP, making 100 -> 101 a very large jump).
/// </summary>
public sealed class InvestorLevelCalculatorTests
{
    private readonly InvestorLevelCalculator _calculator = new();

    [Fact]
    public void Level1_FloorIsZero()
    {
        Assert.Equal(0L, _calculator.XpToReachLevel(1));
        // Levels below 1 are clamped to the level-1 floor.
        Assert.Equal(0L, _calculator.XpToReachLevel(0));
        Assert.Equal(0L, _calculator.XpToReachLevel(-5));
    }

    [Fact]
    public void Thresholds_AreStrictlyIncreasing_AcrossWholeRange()
    {
        // Includes the soft-cap boundary and several levels beyond it.
        long previous = -1L;
        for (var level = 1; level <= 130; level++)
        {
            var floor = _calculator.XpToReachLevel(level);
            Assert.True(floor > previous,
                $"XpToReachLevel({level}) = {floor} should exceed previous {previous}.");
            previous = floor;
        }
    }

    [Fact]
    public void PerLevelCost_IsStrictlyIncreasing_UpToSoftCap()
    {
        long previousCost = -1L;
        for (var level = 1; level < InvestorLevelCalculator.SoftCapLevel; level++)
        {
            var cost = _calculator.XpToReachLevel(level + 1) - _calculator.XpToReachLevel(level);
            Assert.True(cost > previousCost,
                $"Cost for level {level}->{level + 1} = {cost} should exceed previous {previousCost}.");
            previousCost = cost;
        }
    }

    [Fact]
    public void Level100_MatchesOsuFormulaMagnitude()
    {
        // The canonical osu! score-to-level-100 value is ~26,931,190,827.
        var floor100 = _calculator.XpToReachLevel(100);
        Assert.InRange(floor100, 26_931_190_000L, 26_931_191_700L);
    }

    [Fact]
    public void BeyondSoftCap_EachLevelCostsFlat100Billion()
    {
        var floor100 = _calculator.XpToReachLevel(100);
        var floor101 = _calculator.XpToReachLevel(101);
        var floor102 = _calculator.XpToReachLevel(102);

        Assert.Equal(InvestorLevelCalculator.SoftCapXpPerLevel, floor101 - floor100);
        Assert.Equal(InvestorLevelCalculator.SoftCapXpPerLevel, floor102 - floor101);
    }

    [Fact]
    public void SoftCapJump_DwarfsPreSoftCapJump()
    {
        var lastNormalCost = _calculator.XpToReachLevel(100) - _calculator.XpToReachLevel(99);
        var softCapCost = _calculator.XpToReachLevel(101) - _calculator.XpToReachLevel(100);

        // 100 -> 101 must be a very large jump relative to 99 -> 100.
        Assert.True(softCapCost > lastNormalCost * 10,
            $"Soft-cap jump {softCapCost} should dwarf the prior jump {lastNormalCost}.");
    }

    [Fact]
    public void GetProgress_AtZeroXp_IsLevel1WithNoProgress()
    {
        var progress = _calculator.GetProgress(0L);

        Assert.Equal(1, progress.Level);
        Assert.Equal("Novice Investor", progress.Title);
        Assert.Equal(0L, progress.TotalXp);
        Assert.Equal(0L, progress.XpIntoLevel);
        Assert.Equal(_calculator.XpToReachLevel(2), progress.XpForNextLevel);
        Assert.Equal(0d, progress.ProgressToNext);
    }

    [Fact]
    public void GetProgress_NegativeXp_ClampsToLevel1()
    {
        var progress = _calculator.GetProgress(-1_000L);

        Assert.Equal(1, progress.Level);
        Assert.Equal(0L, progress.TotalXp);
        Assert.Equal(0L, progress.XpIntoLevel);
    }

    [Fact]
    public void GetProgress_JustBelowAndAtLevelBoundary_ResolvesCorrectLevel()
    {
        var floor2 = _calculator.XpToReachLevel(2);

        var below = _calculator.GetProgress(floor2 - 1);
        Assert.Equal(1, below.Level);
        Assert.Equal(floor2 - 1, below.XpIntoLevel);

        var at = _calculator.GetProgress(floor2);
        Assert.Equal(2, at.Level);
        Assert.Equal(0L, at.XpIntoLevel);
    }

    [Fact]
    public void GetProgress_DecomposesXpConsistently()
    {
        // Pick an XP value comfortably inside the curve.
        var totalXp = _calculator.XpToReachLevel(30) + 12_345L;
        var progress = _calculator.GetProgress(totalXp);

        Assert.Equal(30, progress.Level);
        Assert.Equal(totalXp, progress.TotalXp);

        var floor = _calculator.XpToReachLevel(progress.Level);
        var nextFloor = _calculator.XpToReachLevel(progress.Level + 1);

        Assert.Equal(totalXp - floor, progress.XpIntoLevel);
        Assert.Equal(nextFloor - floor, progress.XpForNextLevel);
        Assert.InRange(progress.ProgressToNext, 0d, 1d);
        Assert.Equal((double)progress.XpIntoLevel / progress.XpForNextLevel, progress.ProgressToNext, 6);
    }

    [Fact]
    public void GetProgress_InSoftCapRegion_ResolvesLinearLevel()
    {
        var floor100 = _calculator.XpToReachLevel(100);

        var at100 = _calculator.GetProgress(floor100);
        Assert.Equal(100, at100.Level);
        // GetProgress must wire the resolved level through to the title in the soft-cap region.
        Assert.Equal("Market Legend", at100.Title);

        Assert.Equal(101, _calculator.GetProgress(floor100 + InvestorLevelCalculator.SoftCapXpPerLevel).Level);

        var at105 = _calculator.GetProgress(floor100 + (5 * InvestorLevelCalculator.SoftCapXpPerLevel));
        Assert.Equal(105, at105.Level);
        Assert.Equal("Market Legend", at105.Title);
    }

    [Theory]
    [InlineData(1, "Novice Investor")]
    [InlineData(9, "Novice Investor")]
    [InlineData(10, "Retail Trader")]
    [InlineData(24, "Retail Trader")]
    [InlineData(25, "Active Trader")]
    [InlineData(49, "Active Trader")]
    [InlineData(50, "Seasoned Investor")]
    [InlineData(74, "Seasoned Investor")]
    [InlineData(75, "Blue-Chip Trader")]
    [InlineData(99, "Blue-Chip Trader")]
    [InlineData(100, "Market Legend")]
    [InlineData(150, "Market Legend")]
    public void GetTitle_MapsLevelBands(int level, string expectedTitle)
    {
        Assert.Equal(expectedTitle, _calculator.GetTitle(level));
    }
}
