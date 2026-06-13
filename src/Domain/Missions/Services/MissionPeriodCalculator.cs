using System.Globalization;
using OsuStocks.Domain.Missions.Interfaces;
using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Missions.Services;

/// <summary>
/// Resolves mission period windows in UTC. Daily = UTC calendar day; weekly = ISO-8601 week
/// (Monday 00:00 UTC to next Monday 00:00 UTC). Pure and deterministic.
/// </summary>
public sealed class MissionPeriodCalculator : IMissionPeriodCalculator
{
    public MissionPeriod GetPeriod(MissionPeriodType type, DateTimeOffset instant)
    {
        var utc = instant.ToUniversalTime();

        return type switch
        {
            MissionPeriodType.Daily => Daily(utc),
            MissionPeriodType.Weekly => Weekly(utc),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown mission period type."),
        };
    }

    private static MissionPeriod Daily(DateTimeOffset utc)
    {
        var start = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
        var key = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new MissionPeriod(MissionPeriodType.Daily, key, start, start.AddDays(1));
    }

    private static MissionPeriod Weekly(DateTimeOffset utc)
    {
        // ISO-8601: weeks start Monday. Compute the Monday 00:00 UTC of this instant's week.
        var date = utc.Date;
        var isoDayOfWeek = ((int)date.DayOfWeek + 6) % 7; // Mon=0 .. Sun=6
        var monday = date.AddDays(-isoDayOfWeek);
        var start = new DateTimeOffset(monday, TimeSpan.Zero);

        // ISO week-year and week number (NOT calendar year — they diverge near year boundaries).
        var weekYear = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        var key = string.Create(CultureInfo.InvariantCulture, $"{weekYear:D4}-W{week:D2}");

        return new MissionPeriod(MissionPeriodType.Weekly, key, start, start.AddDays(7));
    }
}
