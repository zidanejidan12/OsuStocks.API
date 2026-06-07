namespace OsuStocks.Application.Features.Trading.GetHoldings;

public sealed record GetHoldingsResponse(IReadOnlyList<HoldingItemResponse> Items);

public sealed record HoldingItemResponse(
    Guid HoldingId,
    Guid StockId,
    string? PlayerName,
    int Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string? AvatarUrl = null);
