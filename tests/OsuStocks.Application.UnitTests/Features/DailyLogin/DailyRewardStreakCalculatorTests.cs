using OsuStocks.Application.Features.DailyLogin;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.DailyLogin;

public sealed class DailyRewardStreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 6, 10);
    private const int ScheduleLength = 7;

    [Fact]
    public void AlreadyClaimedToday_ReturnsAlreadyClaimed()
    {
        var result = DailyRewardStreakCalculator.Compute(Today, previousStreak: 3, Today, ScheduleLength);

        Assert.True(result.AlreadyClaimed);
        Assert.Equal(0, result.StreakDay);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(6, 7)]
    public void ConsecutiveDay_WithinCycle_Advances(int previousStreak, int expected)
    {
        var result = DailyRewardStreakCalculator.Compute(Today.AddDays(-1), previousStreak, Today, ScheduleLength);

        Assert.False(result.AlreadyClaimed);
        Assert.Equal(expected, result.StreakDay);
    }

    [Fact]
    public void ConsecutiveDay_AfterFinalDay_WrapsToDayOne()
    {
        var result = DailyRewardStreakCalculator.Compute(Today.AddDays(-1), previousStreak: 7, Today, ScheduleLength);

        Assert.False(result.AlreadyClaimed);
        Assert.Equal(1, result.StreakDay);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(30)]
    public void GapMoreThanOneDay_ResetsToDayOne(int daysAgo)
    {
        var result = DailyRewardStreakCalculator.Compute(Today.AddDays(-daysAgo), previousStreak: 5, Today, ScheduleLength);

        Assert.False(result.AlreadyClaimed);
        Assert.Equal(1, result.StreakDay);
    }

    [Fact]
    public void FirstEverClaim_IsDayOne()
    {
        var result = DailyRewardStreakCalculator.Compute(lastRewardDate: null, previousStreak: 0, Today, ScheduleLength);

        Assert.False(result.AlreadyClaimed);
        Assert.Equal(1, result.StreakDay);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(8, 2)]
    [InlineData(-1, 7)]
    [InlineData(14, 1)]
    public void ConsecutiveDay_OutOfRangePreviousStreak_NormalizesDeterministically(int previousStreak, int expected)
    {
        var result = DailyRewardStreakCalculator.Compute(Today.AddDays(-1), previousStreak, Today, ScheduleLength);

        Assert.False(result.AlreadyClaimed);
        Assert.Equal(expected, result.StreakDay);
        Assert.InRange(result.StreakDay, 1, ScheduleLength);
    }

    [Theory]
    [InlineData(5, 3, 4)]    // cycle of 5, day 3 -> 4
    [InlineData(5, 5, 1)]    // cycle of 5, final day -> wraps to 1
    [InlineData(10, 7, 8)]   // cycle of 10, day 7 -> 8
    [InlineData(1, 1, 1)]    // cycle of 1 always grants day 1
    public void ConsecutiveDay_RespectsScheduleLength(int scheduleLength, int previousStreak, int expected)
    {
        var result = DailyRewardStreakCalculator.Compute(Today.AddDays(-1), previousStreak, Today, scheduleLength);

        Assert.Equal(expected, result.StreakDay);
    }

    [Fact]
    public void FullCycleThenContinue_ProducesOneThroughSevenThenOne()
    {
        DateOnly day = new(2026, 1, 1);
        DateOnly? last = null;
        var streak = 0;
        var observed = new List<int>();

        for (var i = 0; i < 8; i++)
        {
            var result = DailyRewardStreakCalculator.Compute(last, streak, day, ScheduleLength);
            observed.Add(result.StreakDay);
            streak = result.StreakDay;
            last = day;
            day = day.AddDays(1);
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 1 }, observed);
    }

    [Fact]
    public void ConsecutiveAcrossCalendarBoundaries_Advances()
    {
        // Month boundary: Jan 31 -> Feb 1.
        Assert.Equal(3, DailyRewardStreakCalculator
            .Compute(new DateOnly(2026, 1, 31), 2, new DateOnly(2026, 2, 1), ScheduleLength).StreakDay);

        // Year boundary: Dec 31 -> Jan 1.
        Assert.Equal(5, DailyRewardStreakCalculator
            .Compute(new DateOnly(2025, 12, 31), 4, new DateOnly(2026, 1, 1), ScheduleLength).StreakDay);

        // Leap day: Feb 28 -> Feb 29 (2024).
        Assert.Equal(2, DailyRewardStreakCalculator
            .Compute(new DateOnly(2024, 2, 28), 1, new DateOnly(2024, 2, 29), ScheduleLength).StreakDay);

        // Leap day rollover: Feb 29 -> Mar 1 (2024).
        Assert.Equal(3, DailyRewardStreakCalculator
            .Compute(new DateOnly(2024, 2, 29), 2, new DateOnly(2024, 3, 1), ScheduleLength).StreakDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ScheduleLengthLessThanOne_Throws(int scheduleLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DailyRewardStreakCalculator.Compute(null, 0, Today, scheduleLength));
    }
}
