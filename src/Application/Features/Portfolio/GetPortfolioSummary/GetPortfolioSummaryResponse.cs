namespace OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;

public sealed record GetPortfolioSummaryResponse(
    decimal CurrentValue,
    decimal CostBasis,
    decimal ProfitLoss,
    IReadOnlyList<PortfolioHoldingSummaryItem> Holdings);

public sealed record PortfolioHoldingSummaryItem(
    Guid HoldingId,
    Guid StockId,
    string? PlayerName,
    int Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    decimal CostBasis,
    decimal CurrentValue,
    decimal ProfitLoss);
