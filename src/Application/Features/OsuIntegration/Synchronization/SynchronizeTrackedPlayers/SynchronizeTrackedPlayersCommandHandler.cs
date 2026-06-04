using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.OsuIntegration.Interfaces;

namespace OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;

public sealed class SynchronizeTrackedPlayersCommandHandler(IPlayerSynchronizationService playerSynchronizationService)
    : IRequestHandler<SynchronizeTrackedPlayersCommand, Result<SynchronizeTrackedPlayersResponse>>
{
    public async Task<Result<SynchronizeTrackedPlayersResponse>> Handle(
        SynchronizeTrackedPlayersCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await playerSynchronizationService.SynchronizeTrackedPlayersAsync(cancellationToken);
            return Result.Success(new SynchronizeTrackedPlayersResponse(
                summary.TrackedPlayers,
                summary.SnapshotsCreated,
                summary.EventsDetected));
        }
        catch (HttpRequestException)
        {
            return Result.Failure<SynchronizeTrackedPlayersResponse>("OSU_API_UNAVAILABLE", "Failed to synchronize tracked players from osu! API.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<SynchronizeTrackedPlayersResponse>("SYNC_PROCESSING_FAILED", ex.Message);
        }
    }
}
