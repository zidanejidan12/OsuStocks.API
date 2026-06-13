using OsuStocks.Domain.Achievements.Models;
using OsuStocks.Domain.Achievements.Services;
using OsuStocks.Domain.Missions.Models;
using OsuStocks.Domain.Missions.Services;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.Achievements;

/// <summary>
/// Guards the static catalogs against accidental edits: stable unique codes and sane
/// (positive) thresholds/targets/rewards, since codes are persisted on unlock/completion rows.
/// </summary>
public sealed class CatalogSanityTests
{
    [Fact]
    public void AchievementCatalog_HasUniqueCodes_AndPositiveThresholdsAndRewards()
    {
        var all = new AchievementCatalog().All;

        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Select(a => a.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Code));
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.True(a.Threshold > 0, $"{a.Code} threshold must be positive.");
            Assert.True(a.RewardCredits >= 0, $"{a.Code} reward must be non-negative.");
        });
    }

    [Fact]
    public void MissionCatalog_HasUniqueCodes_AndPositiveTargetsAndRewards_AcrossBothCadences()
    {
        var all = new MissionCatalog().All;

        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Select(m => m.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(all, m => m.Period == MissionPeriodType.Daily);
        Assert.Contains(all, m => m.Period == MissionPeriodType.Weekly);
        Assert.All(all, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Code));
            Assert.True(m.Target > 0, $"{m.Code} target must be positive.");
            Assert.True(m.RewardCredits >= 0, $"{m.Code} reward must be non-negative.");
        });
    }

    [Fact]
    public void AchievementMetricsSnapshot_FloorsVolume_AndMapsMetrics()
    {
        var snapshot = new AchievementMetricsSnapshot(
            TotalTrades: 7,
            TotalVolume: 1234.99m,
            DistinctStocksTraded: 3,
            InvestorLevel: 12);

        Assert.Equal(7, snapshot.ValueOf(AchievementMetric.TotalTrades));
        Assert.Equal(1234, snapshot.ValueOf(AchievementMetric.TotalVolume));
        Assert.Equal(3, snapshot.ValueOf(AchievementMetric.DistinctStocksTraded));
        Assert.Equal(12, snapshot.ValueOf(AchievementMetric.InvestorLevel));
    }

    [Fact]
    public void MissionMetricsSnapshot_FloorsVolume_AndMapsMetrics()
    {
        var snapshot = new MissionMetricsSnapshot(
            TradesInPeriod: 4,
            VolumeInPeriod: 50_000.5m,
            DistinctStocksInPeriod: 2);

        Assert.Equal(4, snapshot.ValueOf(MissionMetric.TradesInPeriod));
        Assert.Equal(50_000, snapshot.ValueOf(MissionMetric.VolumeInPeriod));
        Assert.Equal(2, snapshot.ValueOf(MissionMetric.DistinctStocksInPeriod));
    }
}
