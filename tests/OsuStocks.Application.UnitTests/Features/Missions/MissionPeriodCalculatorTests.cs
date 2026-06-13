using OsuStocks.Domain.Missions.Models;
using OsuStocks.Domain.Missions.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Missions;

/// <summary>
/// Verifies mission period resolution in UTC: daily = UTC calendar day, weekly = ISO-8601 week
/// (Monday start), with correct keys, half-open windows, and ISO week-year rollover handling.
/// </summary>
public sealed class MissionPeriodCalculatorTests
{
    private readonly MissionPeriodCalculator _calculator = new();

    [Fact]
    public void Daily_ResolvesUtcDayWindowAndKey()
    {
        var instant = new DateTimeOffset(2026, 6, 14, 15, 30, 0, TimeSpan.Zero);

        var period = _calculator.GetPeriod(MissionPeriodType.Daily, instant);

        Assert.Equal("2026-06-14", period.Key);
        Assert.Equal(new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero), period.Start);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero), period.End);
    }

    [Fact]
    public void Daily_NormalizesNonUtcInstantToUtcDay()
    {
        // 2026-06-14T01:00:00+07:00 == 2026-06-13T18:00:00Z, so the UTC day is the 13th.
        var instant = new DateTimeOffset(2026, 6, 14, 1, 0, 0, TimeSpan.FromHours(7));

        var period = _calculator.GetPeriod(MissionPeriodType.Daily, instant);

        Assert.Equal("2026-06-13", period.Key);
    }

    [Fact]
    public void Daily_WindowIsHalfOpen_BoundaryBelongsToNextDay()
    {
        var endOfDay = new DateTimeOffset(2026, 6, 14, 23, 59, 59, 999, TimeSpan.Zero);
        var midnight = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("2026-06-14", _calculator.GetPeriod(MissionPeriodType.Daily, endOfDay).Key);
        Assert.Equal("2026-06-15", _calculator.GetPeriod(MissionPeriodType.Daily, midnight).Key);
        // The previous day's exclusive End equals the next day's inclusive Start.
        Assert.Equal(
            _calculator.GetPeriod(MissionPeriodType.Daily, endOfDay).End,
            _calculator.GetPeriod(MissionPeriodType.Daily, midnight).Start);
    }

    [Fact]
    public void Weekly_ResolvesMondayStartAndSevenDayWindow()
    {
        // 2026-06-14 is a Sunday; its ISO week started Monday 2026-06-08.
        var sunday = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

        var period = _calculator.GetPeriod(MissionPeriodType.Weekly, sunday);

        Assert.Equal(new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero), period.Start);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero), period.End);
        Assert.Equal(DayOfWeek.Monday, period.Start.DayOfWeek);
    }

    [Fact]
    public void Weekly_MondayAndSundayOfSameWeekShareKeyAndWindow()
    {
        var monday = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);
        var sunday = new DateTimeOffset(2026, 6, 14, 23, 0, 0, TimeSpan.Zero);

        var a = _calculator.GetPeriod(MissionPeriodType.Weekly, monday);
        var b = _calculator.GetPeriod(MissionPeriodType.Weekly, sunday);

        Assert.Equal(a.Key, b.Key);
        Assert.Equal(a.Start, b.Start);
        Assert.Equal(a.End, b.End);
    }

    [Theory]
    // 2026-01-01 (Thursday) falls in ISO week 2026-W01.
    [InlineData(2026, 1, 1, "2026-W01")]
    // 2025-12-29 (Monday) starts ISO week 2026-W01 (week-year ahead of calendar year).
    [InlineData(2025, 12, 29, "2026-W01")]
    // 2021-01-01 (Friday) belongs to ISO week 2020-W53 (week-year behind calendar year).
    [InlineData(2021, 1, 1, "2020-W53")]
    public void Weekly_UsesIsoWeekYear_NotCalendarYear(int year, int month, int day, string expectedKey)
    {
        var instant = new DateTimeOffset(year, month, day, 10, 0, 0, TimeSpan.Zero);

        var period = _calculator.GetPeriod(MissionPeriodType.Weekly, instant);

        Assert.Equal(expectedKey, period.Key);
        Assert.Equal(DayOfWeek.Monday, period.Start.DayOfWeek);
    }
}
