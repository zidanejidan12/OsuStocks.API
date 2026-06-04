using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;

public sealed record SearchOsuPlayersQuery(
    string Query,
    int Limit = 10) : IRequest<Result<SearchOsuPlayersResponse>>;
