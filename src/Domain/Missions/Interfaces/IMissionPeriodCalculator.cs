using OsuStocks.Domain.Missions.Models;

namespace OsuStocks.Domain.Missions.Interfaces;

/// <summary>
/// Pure resolution of mission period windows from a UTC instant. Daily periods are UTC calendar
/// days; weekly periods are ISO-8601 weeks (Monday 00:00 UTC start).
/// </summary>
public interface IMissionPeriodCalculator
{
    /// <summary>Resolves the period of the given type that contains <paramref name="instant"/>.</summary>
    MissionPeriod GetPeriod(MissionPeriodType type, DateTimeOffset instant);
}
