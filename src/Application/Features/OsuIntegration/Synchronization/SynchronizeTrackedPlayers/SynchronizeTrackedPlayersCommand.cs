using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;

public sealed record SynchronizeTrackedPlayersCommand(TrackingTier? Tier = null)
    : IRequest<Result<SynchronizeTrackedPlayersResponse>>;
