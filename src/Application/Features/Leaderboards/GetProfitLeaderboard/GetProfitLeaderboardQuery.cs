using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Leaderboards.GetProfitLeaderboard;

public sealed record GetProfitLeaderboardQuery(
    string? Period,
    int Page,
    int PageSize)
    : IRequest<Result<GetProfitLeaderboardResponse>>;
