using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Portfolio.GetPortfolioSummary;

public sealed class GetPortfolioSummaryQueryHandler(IPortfolioReadRepository portfolioReadRepository)
    : IRequestHandler<GetPortfolioSummaryQuery, Result<GetPortfolioSummaryResponse>>
{
    public async Task<Result<GetPortfolioSummaryResponse>> Handle(
        GetPortfolioSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var holdings = await portfolioReadRepository
            .GetPortfolioSummaryHoldingsByUserIdAsync(request.UserId, cancellationToken);

        var items = holdings
            .Select(x => new PortfolioHoldingSummaryItem(
                x.HoldingId,
                x.StockId,
                x.PlayerName,
                x.Quantity,
                x.AveragePrice,
                x.CurrentPrice,
                x.CostBasis,
                x.CurrentValue,
                x.ProfitLoss))
            .ToList();

        var totalCurrentValue = holdings.Sum(x => x.CurrentValue);
        var totalCostBasis = holdings.Sum(x => x.CostBasis);

        return Result.Success(new GetPortfolioSummaryResponse(
            totalCurrentValue,
            totalCostBasis,
            totalCurrentValue - totalCostBasis,
            items));
    }
}
