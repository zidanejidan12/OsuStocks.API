using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Market.Events;

public sealed record PriceChanged(
    Guid StockId,
    decimal PreviousPrice,
    decimal NewPrice,
    PriceChangeReason Reason,
    DateTimeOffset OccurredAt);
