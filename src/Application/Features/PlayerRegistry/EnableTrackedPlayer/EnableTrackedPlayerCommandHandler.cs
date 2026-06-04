using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.PlayerRegistry.EnableTrackedPlayer;

public sealed class EnableTrackedPlayerCommandHandler(
    ITrackedPlayerRepository trackedPlayerRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<EnableTrackedPlayerCommand, Result<EnableTrackedPlayerResponse>>
{
    public async Task<Result<EnableTrackedPlayerResponse>> Handle(
        EnableTrackedPlayerCommand request,
        CancellationToken cancellationToken)
    {
        var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(request.TrackedPlayerId, cancellationToken);
        if (trackedPlayer is null)
        {
            return Result.Failure<EnableTrackedPlayerResponse>("NOT_FOUND", "Tracked player was not found.");
        }

        if (!trackedPlayer.IsActive)
        {
            trackedPlayer.IsActive = true;
            trackedPlayer.UpdatedAt = DateTimeOffset.UtcNow;
            trackedPlayer.UpdatedBy = string.IsNullOrWhiteSpace(request.Actor) ? "admin" : request.Actor;

            trackedPlayerRepository.Update(trackedPlayer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new EnableTrackedPlayerResponse(trackedPlayer.Id, trackedPlayer.IsActive));
    }
}
