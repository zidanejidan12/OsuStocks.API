using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Entities;

public sealed class Trade
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid StockId { get; set; }
    public TradeType TradeType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }

    public User User { get; set; } = null!;
    public PlayerStock Stock { get; set; } = null!;
}
