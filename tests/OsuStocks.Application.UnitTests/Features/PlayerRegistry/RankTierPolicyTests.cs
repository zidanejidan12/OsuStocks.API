using OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;
using OsuStocks.Domain.Common.Enums;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.PlayerRegistry;

public sealed class RankTierPolicyTests
{
    [Theory]
    [InlineData(1, TrackingTier.Tier1)]
    [InlineData(50, TrackingTier.Tier1)]
    [InlineData(51, TrackingTier.Tier2)]
    [InlineData(500, TrackingTier.Tier2)]
    [InlineData(501, TrackingTier.Tier3)]
    [InlineData(2000, TrackingTier.Tier3)]
    [InlineData(2001, TrackingTier.Tier4)]
    [InlineData(5000, TrackingTier.Tier4)]
    public void TierForRank_AssignsBandByRank(int rank, TrackingTier expected)
    {
        Assert.Equal(expected, RankTierPolicy.TierForRank(rank));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void TierForRank_UnrankedOrInvalid_GoesToSlowestTier(int? rank)
    {
        Assert.Equal(TrackingTier.Tier4, RankTierPolicy.TierForRank(rank));
    }
}
