namespace OsuStocks.Domain.Market.Events;

public sealed record SellOrderExecuted(
    Guid UserId,
    Guid StockId,
    int Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
