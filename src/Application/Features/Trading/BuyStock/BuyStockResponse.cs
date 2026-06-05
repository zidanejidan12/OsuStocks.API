namespace OsuStocks.Application.Features.Trading.BuyStock;

public sealed record BuyStockResponse(Guid TradeId, decimal UnitPrice, decimal TotalAmount);
