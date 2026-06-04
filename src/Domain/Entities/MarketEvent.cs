namespace OsuStocks.Domain.Entities;

public sealed class MarketEvent
{
    public Guid Id { get; set; }
    public Guid StockId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public PlayerStock Stock { get; set; } = null!;
}
