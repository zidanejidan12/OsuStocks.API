namespace OsuStocks.Domain.Entities;

public sealed class Portfolio
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
}
