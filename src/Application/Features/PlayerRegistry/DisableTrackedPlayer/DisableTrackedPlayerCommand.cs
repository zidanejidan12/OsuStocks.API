using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;

public sealed record DisableTrackedPlayerCommand(
    Guid TrackedPlayerId,
    string? Actor) : IRequest<Result<DisableTrackedPlayerResponse>>;
