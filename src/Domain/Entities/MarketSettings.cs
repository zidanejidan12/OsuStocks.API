namespace OsuStocks.Domain.Entities;

public sealed class MarketSettings
{
    public Guid Id { get; set; }
    public decimal PpMultiplier { get; set; } = 1m;
    public decimal TradeMultiplier { get; set; } = 1m;
    public decimal DecayMultiplier { get; set; } = 1m;
    public bool IsMaintenanceMode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
