using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.PlayerRegistry.DisableTrackedPlayer;

public sealed class DisableTrackedPlayerCommandHandler(
    ITrackedPlayerRepository trackedPlayerRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<DisableTrackedPlayerCommand, Result<DisableTrackedPlayerResponse>>
{
    public async Task<Result<DisableTrackedPlayerResponse>> Handle(
        DisableTrackedPlayerCommand request,
        CancellationToken cancellationToken)
    {
        var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(request.TrackedPlayerId, cancellationToken);
        if (trackedPlayer is null)
        {
            return Result.Failure<DisableTrackedPlayerResponse>("NOT_FOUND", "Tracked player was not found.");
        }

        if (trackedPlayer.IsActive)
        {
            trackedPlayer.IsActive = false;
            trackedPlayer.UpdatedAt = DateTimeOffset.UtcNow;
            trackedPlayer.UpdatedBy = string.IsNullOrWhiteSpace(request.Actor) ? "admin" : request.Actor;

            trackedPlayerRepository.Update(trackedPlayer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new DisableTrackedPlayerResponse(trackedPlayer.Id, trackedPlayer.IsActive));
    }
}
