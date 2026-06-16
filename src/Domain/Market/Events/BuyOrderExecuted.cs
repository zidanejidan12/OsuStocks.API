namespace OsuStocks.Domain.Market.Events;

public sealed record BuyOrderExecuted(
    Guid UserId,
    Guid StockId,
    decimal Quantity,
    decimal UnitPrice,
    DateTimeOffset OccurredAt);
