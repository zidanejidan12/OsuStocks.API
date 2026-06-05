using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Models;

public sealed record TradeHistoryReadModel(
    Guid TradeId,
    Guid StockId,
    TradeType TradeType,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTimeOffset ExecutedAt,
    string? PlayerName);
