namespace OsuStocks.Domain.Missions.Models;

/// <summary>
/// A per-period user metric a mission is measured against, derived on demand from the user's
/// trades within the period window.
/// </summary>
public enum MissionMetric
{
    /// <summary>Number of trades executed in the period.</summary>
    TradesInPeriod = 1,

    /// <summary>Gross traded credits in the period.</summary>
    VolumeInPeriod = 2,

    /// <summary>Distinct stocks traded in the period.</summary>
    DistinctStocksInPeriod = 3,
}
