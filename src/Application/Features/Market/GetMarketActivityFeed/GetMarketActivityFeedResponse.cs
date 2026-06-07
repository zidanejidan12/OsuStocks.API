namespace OsuStocks.Application.Features.Market.GetMarketActivityFeed;

public sealed record GetMarketActivityFeedResponse(
    IReadOnlyList<MarketActivityItemResponse> Items,
    int Page,
    int PageSize);

public sealed record MarketActivityItemResponse(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    string Reason,
    string Description,
    decimal PercentChange,
    decimal NewPrice,
    DateTimeOffset OccurredAt);
