using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed record ListTrackedPlayersQuery(bool? IsActive) : IRequest<Result<ListTrackedPlayersResponse>>;
