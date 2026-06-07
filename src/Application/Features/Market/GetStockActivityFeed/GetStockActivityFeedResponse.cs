namespace OsuStocks.Application.Features.Market.GetStockActivityFeed;

public sealed record GetStockActivityFeedResponse(
    IReadOnlyList<StockActivityItemResponse> Items,
    int Page,
    int PageSize);

public sealed record StockActivityItemResponse(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    string Reason,
    string Description,
    decimal PercentChange,
    decimal NewPrice,
    DateTimeOffset OccurredAt);
