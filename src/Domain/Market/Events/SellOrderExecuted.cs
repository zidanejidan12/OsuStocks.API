namespace OsuStocks.Domain.Market.Events;

public sealed record SellOrderExecuted(
    Guid StockId,
    int Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
