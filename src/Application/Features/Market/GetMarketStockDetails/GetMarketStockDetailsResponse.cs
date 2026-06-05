namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed record GetMarketStockDetailsResponse(
    Guid StockId,
    string PlayerName,
    decimal CurrentPrice,
    long Volume,
    decimal PriceChange24h);
