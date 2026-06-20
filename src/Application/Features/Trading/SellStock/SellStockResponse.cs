namespace OsuStocks.Application.Features.Trading.SellStock;

public sealed record SellStockResponse(Guid TradeId, decimal Quantity, decimal UnitPrice, decimal TotalAmount, decimal Fee);
