using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.PlayerRegistry.AddTrackedPlayer;

public sealed record AddTrackedPlayerCommand(
    long OsuUserId,
    TrackingTier TrackingTier,
    string? Actor) : IRequest<Result<AddTrackedPlayerResponse>>;
