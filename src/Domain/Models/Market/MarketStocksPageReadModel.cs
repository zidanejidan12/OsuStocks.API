namespace OsuStocks.Domain.Models.Market;

public sealed record MarketStocksPageReadModel(
    IReadOnlyList<MarketStockListItemReadModel> Items,
    int TotalCount);
