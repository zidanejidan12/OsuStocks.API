using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;

public sealed class GetPortfolioSummaryQueryHandler(
    IPortfolioRepository portfolioRepository,
    IHoldingRepository holdingRepository,
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository)
    : IRequestHandler<GetPortfolioSummaryQuery, Result<GetPortfolioSummaryResponse>>
{
    public async Task<Result<GetPortfolioSummaryResponse>> Handle(
        GetPortfolioSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (portfolio is null)
        {
            return Result.Success(new GetPortfolioSummaryResponse(0m, 0m, 0m, []));
        }

        var holdings = await holdingRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
        var items = new List<PortfolioHoldingSummaryItem>(holdings.Count);

        decimal totalCurrentValue = 0m;
        decimal totalCostBasis = 0m;

        foreach (var holding in holdings)
        {
            var stock = await playerStockRepository.GetByIdAsync(holding.StockId, cancellationToken);
            var currentPrice = stock?.CurrentPrice ?? 0m;
            var currentValue = currentPrice * holding.Quantity;
            var costBasis = holding.AveragePrice * holding.Quantity;
            var profitLoss = currentValue - costBasis;

            string? playerName = null;
            if (stock is not null)
            {
                var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken);
                playerName = trackedPlayer?.Username;
            }

            totalCurrentValue += currentValue;
            totalCostBasis += costBasis;

            items.Add(new PortfolioHoldingSummaryItem(
                holding.Id,
                holding.StockId,
                playerName,
                holding.Quantity,
                holding.AveragePrice,
                currentPrice,
                costBasis,
                currentValue,
                profitLoss));
        }

        return Result.Success(new GetPortfolioSummaryResponse(
            totalCurrentValue,
            totalCostBasis,
            totalCurrentValue - totalCostBasis,
            items));
    }
}
