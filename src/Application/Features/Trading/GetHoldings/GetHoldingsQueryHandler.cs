using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Trading.GetHoldings;

public sealed class GetHoldingsQueryHandler(IPortfolioReadRepository portfolioReadRepository)
    : IRequestHandler<GetHoldingsQuery, Result<GetHoldingsResponse>>
{
    public async Task<Result<GetHoldingsResponse>> Handle(GetHoldingsQuery request, CancellationToken cancellationToken)
    {
        var holdings = await portfolioReadRepository.GetHoldingsByUserIdAsync(request.UserId, cancellationToken);

        var items = holdings
            .Select(x => new HoldingItemResponse(
                x.HoldingId,
                x.StockId,
                x.PlayerName,
                x.Quantity,
                x.AveragePrice,
                x.CurrentPrice,
                x.AvatarUrl))
            .ToList();

        return Result.Success(new GetHoldingsResponse(items));
    }
}
