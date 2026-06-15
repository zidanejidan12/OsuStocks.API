namespace OsuStocks.Application.Features.Market.GetLiveMovers;

public sealed record GetLiveMoversResponse(
    IReadOnlyList<LiveMoverResponse> Items);

public sealed record LiveMoverResponse(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    decimal CurrentPrice,
    decimal PriceChange24h);
