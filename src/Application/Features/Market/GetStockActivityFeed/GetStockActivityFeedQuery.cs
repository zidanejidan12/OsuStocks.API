using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetStockActivityFeed;

public sealed record GetStockActivityFeedQuery(
    Guid StockId,
    int Page,
    int PageSize)
    : IRequest<Result<GetStockActivityFeedResponse>>;
