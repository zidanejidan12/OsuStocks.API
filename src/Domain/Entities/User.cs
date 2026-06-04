using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public long OsuUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public Wallet? Wallet { get; set; }
    public Portfolio? Portfolio { get; set; }
    public ICollection<Trade> Trades { get; set; } = new List<Trade>();
}
