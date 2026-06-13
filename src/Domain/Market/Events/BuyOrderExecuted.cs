namespace OsuStocks.Domain.Market.Events;

public sealed record BuyOrderExecuted(
    Guid UserId,
    Guid StockId,
    int Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
