namespace OsuStocks.Domain.Market.Events;

public sealed record BuyOrderExecuted(
    Guid StockId,
    int Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
