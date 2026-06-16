namespace OsuStocks.Domain.Market.Events;

public sealed record SellOrderExecuted(
    Guid UserId,
    Guid StockId,
    decimal Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
