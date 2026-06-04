namespace OsuStocks.Domain.Entities;

public sealed class PlayerStock
{
    public Guid Id { get; set; }
    public Guid TrackedPlayerId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal DemandScore { get; set; }
    public decimal PerformanceScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public TrackedPlayer TrackedPlayer { get; set; } = null!;
    public ICollection<StockPriceHistory> PriceHistoryEntries { get; set; } = new List<StockPriceHistory>();
    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<Trade> Trades { get; set; } = new List<Trade>();
    public ICollection<MarketEvent> MarketEvents { get; set; } = new List<MarketEvent>();
}
