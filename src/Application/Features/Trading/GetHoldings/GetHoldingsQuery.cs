using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Trading.GetHoldings;

public sealed record GetHoldingsQuery(Guid UserId)
    : IRequest<Result<GetHoldingsResponse>>;
