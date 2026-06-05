using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Trading.GetTradeHistory;

public sealed record GetTradeHistoryQuery(Guid UserId, int Page = 1, int PageSize = 25)
    : IRequest<Result<GetTradeHistoryResponse>>;
