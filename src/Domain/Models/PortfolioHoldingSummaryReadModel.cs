namespace OsuStocks.Domain.Models;

public sealed record PortfolioHoldingSummaryReadModel(
    Guid HoldingId,
    Guid StockId,
    string? PlayerName,
    int Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    decimal CostBasis,
    decimal CurrentValue,
    decimal ProfitLoss);
