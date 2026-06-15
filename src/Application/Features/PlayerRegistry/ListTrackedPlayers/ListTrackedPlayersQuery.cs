using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed record ListTrackedPlayersQuery(
    bool? IsActive,
    string? Search = null,
    int Page = 1,
    int PageSize = 25) : IRequest<Result<ListTrackedPlayersResponse>>;
