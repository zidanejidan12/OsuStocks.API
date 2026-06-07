using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetTrending;

public sealed record GetTrendingQuery(
    string? Window,
    int Limit)
    : IRequest<Result<GetTrendingResponse>>;
