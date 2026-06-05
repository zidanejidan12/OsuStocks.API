namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStocksQuerySpec(
    int Page,
    int PageSize,
    string? Sort,
    string? Search);
