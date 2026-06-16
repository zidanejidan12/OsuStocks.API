namespace OsuStocks.Application.Features.Trading.GetTradeHistory;

public sealed record GetTradeHistoryResponse(IReadOnlyList<TradeHistoryItemResponse> Items);

public sealed record TradeHistoryItemResponse(
    Guid TradeId,
    Guid StockId,
    string TradeType,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTimeOffset ExecutedAt,
    string? PlayerName,
    string? AvatarUrl = null);
