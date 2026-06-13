using OsuStocks.Domain.Common.Interfaces;

namespace OsuStocks.Domain.Entities;

/// <summary>
/// Per-user investor progression aggregate. Tracks lifetime experience (XP) earned from
/// trading activity and the derived level. Level is denormalised from <see cref="TotalXp"/>
/// (the source of truth) so reads and level-up detection stay cheap.
/// </summary>
public sealed class InvestorProfile : IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Lifetime experience earned. Source of truth for the level.</summary>
    public long TotalXp { get; set; }

    /// <summary>Denormalised current level derived from <see cref="TotalXp"/> (minimum 1).</summary>
    public int Level { get; set; } = 1;

    public DateTimeOffset? LastLevelUpAt { get; set; }

    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public User User { get; set; } = null!;
}
