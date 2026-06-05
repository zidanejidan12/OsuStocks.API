namespace OsuStocks.Application.Features.Market.GetMarketStocks;

public sealed record GetMarketStocksResponse(
    IReadOnlyList<MarketStockListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record MarketStockListItemResponse(
    Guid StockId,
    string PlayerName,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h);
