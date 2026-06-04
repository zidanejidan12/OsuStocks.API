using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.PlayerRegistry.ListTrackedPlayers;

public sealed class ListTrackedPlayersQueryHandler(ITrackedPlayerRepository trackedPlayerRepository)
    : IRequestHandler<ListTrackedPlayersQuery, Result<ListTrackedPlayersResponse>>
{
    public async Task<Result<ListTrackedPlayersResponse>> Handle(
        ListTrackedPlayersQuery request,
        CancellationToken cancellationToken)
    {
        var trackedPlayers = await trackedPlayerRepository.GetAllAsync(request.IsActive, cancellationToken);

        var items = trackedPlayers
            .Select(player => new TrackedPlayerListItemResponse(
                player.Id,
                player.OsuUserId,
                player.Username,
                player.TrackingTier.ToString(),
                player.IsActive,
                player.CreatedAt,
                player.UpdatedAt))
            .ToList();

        return Result.Success(new ListTrackedPlayersResponse(items));
    }
}
