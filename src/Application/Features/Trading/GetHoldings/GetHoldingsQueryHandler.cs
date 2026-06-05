using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.GetHoldings;

public sealed class GetHoldingsQueryHandler(
    IPortfolioRepository portfolioRepository,
    IHoldingRepository holdingRepository,
    IPlayerStockRepository playerStockRepository,
    ITrackedPlayerRepository trackedPlayerRepository)
    : IRequestHandler<GetHoldingsQuery, Result<GetHoldingsResponse>>
{
    public async Task<Result<GetHoldingsResponse>> Handle(GetHoldingsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (portfolio is null)
        {
            return Result.Success(new GetHoldingsResponse([]));
        }

        var holdings = await holdingRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
        var items = new List<HoldingItemResponse>(holdings.Count);

        foreach (var holding in holdings)
        {
            var stock = await playerStockRepository.GetByIdAsync(holding.StockId, cancellationToken);
            string? playerName = null;
            var currentPrice = stock?.CurrentPrice ?? 0m;

            if (stock is not null)
            {
                var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(stock.TrackedPlayerId, cancellationToken);
                playerName = trackedPlayer?.Username;
            }

            items.Add(new HoldingItemResponse(
                holding.Id,
                holding.StockId,
                playerName,
                holding.Quantity,
                holding.AveragePrice,
                currentPrice));
        }

        return Result.Success(new GetHoldingsResponse(items));
    }
}
