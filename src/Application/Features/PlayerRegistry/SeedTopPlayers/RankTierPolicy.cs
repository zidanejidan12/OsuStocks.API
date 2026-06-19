using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.PlayerRegistry.SeedTopPlayers;

/// <summary>
/// Maps a player's global rank to a sync <see cref="TrackingTier"/>. Higher-ranked players land in
/// faster-syncing tiers so their stocks stay fresh, while the long tail syncs less often — keeping
/// the whole tracked set within the shared osu! API request budget. Unranked players go to the
/// slowest tier.
/// </summary>
public static class RankTierPolicy
{
    public static TrackingTier TierForRank(int? globalRank) => globalRank switch
    {
        null or <= 0 => TrackingTier.Tier4,
        <= 50 => TrackingTier.Tier1,
        <= 500 => TrackingTier.Tier2,
        <= 2000 => TrackingTier.Tier3,
        _ => TrackingTier.Tier4
    };
}
