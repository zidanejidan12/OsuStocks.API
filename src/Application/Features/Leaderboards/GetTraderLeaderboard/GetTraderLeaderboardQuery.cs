using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Leaderboards.GetTraderLeaderboard;

public sealed record GetTraderLeaderboardQuery(
    string? Period,
    int Page,
    int PageSize)
    : IRequest<Result<GetTraderLeaderboardResponse>>;
