using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Domain.Models;

/// <summary>A trade row enriched with the trader's identity, for the admin transaction monitor.</summary>
public sealed record AdminTradeReadModel(
    Guid TradeId,
    Guid UserId,
    string Username,
    string? AvatarUrl,
    Guid StockId,
    string? PlayerName,
    TradeType TradeType,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTimeOffset ExecutedAt);
