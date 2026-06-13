namespace OsuStocks.Domain.Missions.Models;

/// <summary>
/// A resolved period window for a mission cadence: a stable <see cref="Key"/> plus a half-open
/// UTC time window [Start, End) used to filter trades.
/// </summary>
/// <param name="Type">The cadence this window is for.</param>
/// <param name="Key">Stable identifier: daily "yyyy-MM-dd" or weekly ISO "yyyy-'W'ww".</param>
/// <param name="Start">Inclusive UTC start of the window.</param>
/// <param name="End">Exclusive UTC end of the window.</param>
public sealed record MissionPeriod(
    MissionPeriodType Type,
    string Key,
    DateTimeOffset Start,
    DateTimeOffset End);
