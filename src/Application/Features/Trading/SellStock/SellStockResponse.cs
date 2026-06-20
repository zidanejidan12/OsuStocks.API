namespace OsuStocks.Application.Features.Trading.SellStock;

public sealed record SellStockResponse(Guid TradeId, decimal UnitPrice, decimal TotalAmount, decimal Fee);
