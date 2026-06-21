using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Entities;

public sealed class TrackedPlayer
{
    public Guid Id { get; set; }
    public long OsuUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? CountryCode { get; set; }
    public TrackingTier TrackingTier { get; set; } = TrackingTier.Tier3;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? LastInactivityDecayAt { get; set; }

    public PlayerStock? Stock { get; set; }
    public ICollection<PlayerSnapshot> Snapshots { get; set; } = new List<PlayerSnapshot>();
}
