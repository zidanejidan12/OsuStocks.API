using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Entities;

public sealed class StockPriceHistory
{
    public Guid Id { get; set; }
    public Guid StockId { get; set; }
    public decimal PreviousPrice { get; set; }
    public decimal NewPrice { get; set; }
    public PriceChangeReason Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public PlayerStock Stock { get; set; } = null!;
}
