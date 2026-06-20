namespace OsuStocks.Domain.Entities;

public sealed class MarketSettings
{
    public Guid Id { get; set; }
    public decimal PpMultiplier { get; set; } = 1m;
    public decimal TradeMultiplier { get; set; } = 1m;
    public decimal DecayMultiplier { get; set; } = 1m;
    // Scales the progressive trade fee live (1 = configured rates, 0 = fees off, 2 = double). The
    // bracket schedule itself lives in MarketEngine config; this is the admin's live magnitude knob.
    public decimal TradeFeeMultiplier { get; set; } = 1m;
    public bool IsMaintenanceMode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
