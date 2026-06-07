using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketActivityFeed;

public sealed record GetMarketActivityFeedQuery(
    int Page,
    int PageSize,
    string? Reason)
    : IRequest<Result<GetMarketActivityFeedResponse>>;
