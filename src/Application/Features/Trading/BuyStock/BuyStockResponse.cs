namespace OsuStocks.Application.Features.Trading.BuyStock;

public sealed record BuyStockResponse(Guid TradeId, decimal Quantity, decimal UnitPrice, decimal TotalAmount, decimal Fee);
