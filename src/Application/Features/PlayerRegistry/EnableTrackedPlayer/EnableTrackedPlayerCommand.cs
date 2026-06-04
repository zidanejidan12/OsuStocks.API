using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;

public sealed record EnableTrackedPlayerCommand(
    Guid TrackedPlayerId,
    string? Actor) : IRequest<Result<EnableTrackedPlayerResponse>>;
