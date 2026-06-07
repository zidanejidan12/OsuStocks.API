namespace OsuStocks.Domain.Entities;

public sealed class WealthSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public decimal Wealth { get; set; }
    public decimal NetDeposits { get; set; }
    public decimal Profit { get; set; }
}
