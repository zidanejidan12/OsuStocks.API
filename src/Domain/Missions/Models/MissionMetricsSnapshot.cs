namespace OsuStocks.Domain.Missions.Models;

/// <summary>
/// A user's per-period values for every mission metric, derived from trades in the period window.
/// </summary>
public sealed record MissionMetricsSnapshot(
    long TradesInPeriod,
    decimal VolumeInPeriod,
    int DistinctStocksInPeriod)
{
    /// <summary>Current value of the given metric as a comparable whole number.</summary>
    public long ValueOf(MissionMetric metric) => metric switch
    {
        MissionMetric.TradesInPeriod => TradesInPeriod,
        MissionMetric.VolumeInPeriod => (long)decimal.Floor(VolumeInPeriod),
        MissionMetric.DistinctStocksInPeriod => DistinctStocksInPeriod,
        _ => 0L,
    };
}
