namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminTrades;

public sealed record GetAdminTradesResponse(
    IReadOnlyList<AdminTradeItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminTradeItemResponse(
    Guid TradeId,
    Guid UserId,
    string Username,
    string? AvatarUrl,
    Guid StockId,
    string? PlayerName,
    string TradeType,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTimeOffset ExecutedAt);
