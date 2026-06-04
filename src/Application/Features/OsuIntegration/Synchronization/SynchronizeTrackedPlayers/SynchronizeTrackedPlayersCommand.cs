using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;

public sealed record SynchronizeTrackedPlayersCommand
    : IRequest<Result<SynchronizeTrackedPlayersResponse>>;
