using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Leaderboards.GetWealthLeaderboard;

public sealed record GetWealthLeaderboardQuery(
    string? Period,
    int Page,
    int PageSize)
    : IRequest<Result<GetWealthLeaderboardResponse>>;
