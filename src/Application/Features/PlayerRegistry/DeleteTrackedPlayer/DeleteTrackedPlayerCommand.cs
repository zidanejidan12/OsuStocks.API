using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.PlayerRegistry.DeleteTrackedPlayer;

public sealed record DeleteTrackedPlayerCommand(
    Guid TrackedPlayerId) : IRequest<Result<DeleteTrackedPlayerResponse>>;
