namespace OsuStocks.Domain.Entities;

public sealed class PlayerSnapshot
{
    public Guid Id { get; set; }
    public Guid TrackedPlayerId { get; set; }
    public decimal CurrentPp { get; set; }
    public int? GlobalRank { get; set; }
    public long? TopScoreId { get; set; }
    public decimal? TopScorePp { get; set; }
    public DateTimeOffset CapturedAt { get; set; }

    public TrackedPlayer TrackedPlayer { get; set; } = null!;
}
