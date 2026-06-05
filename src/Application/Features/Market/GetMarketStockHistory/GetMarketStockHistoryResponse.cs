namespace OsuStocks.Application.Features.Market.GetMarketStockHistory;

public sealed record GetMarketStockHistoryResponse(IReadOnlyList<MarketStockHistoryPointResponse> Items);

public sealed record MarketStockHistoryPointResponse(
    DateTimeOffset Timestamp,
    decimal Price);
