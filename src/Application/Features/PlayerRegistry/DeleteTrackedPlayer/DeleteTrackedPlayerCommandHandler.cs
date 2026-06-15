using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.PlayerRegistry.DeleteTrackedPlayer;

public sealed class DeleteTrackedPlayerCommandHandler(
    ITrackedPlayerRepository trackedPlayerRepository,
    IPlayerStockRepository playerStockRepository,
    ITradeRepository tradeRepository,
    IHoldingRepository holdingRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<DeleteTrackedPlayerCommand, Result<DeleteTrackedPlayerResponse>>
{
    public async Task<Result<DeleteTrackedPlayerResponse>> Handle(
        DeleteTrackedPlayerCommand request,
        CancellationToken cancellationToken)
    {
        var trackedPlayer = await trackedPlayerRepository.GetByIdAsync(request.TrackedPlayerId, cancellationToken);
        if (trackedPlayer is null)
        {
            return Result.Failure<DeleteTrackedPlayerResponse>("NOT_FOUND", "Tracked player was not found.");
        }

        var stock = await playerStockRepository.GetByTrackedPlayerIdAsync(trackedPlayer.Id, cancellationToken);
        if (stock is not null)
        {
            var hasTrades = await tradeRepository.ExistsByStockAsync(stock.Id, cancellationToken);
            var heldQuantity = await holdingRepository.GetTotalQuantityByStockAsync(stock.Id, cancellationToken);

            if (hasTrades || heldQuantity > 0)
            {
                return Result.Failure<DeleteTrackedPlayerResponse>(
                    "CONFLICT",
                    "Player has trading history; disable it instead of deleting.");
            }
        }

        // Removing the tracked player cascades to its stock, snapshots, price history and market events
        // via FK ON DELETE CASCADE configured in the EF model.
        trackedPlayerRepository.Remove(trackedPlayer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new DeleteTrackedPlayerResponse(trackedPlayer.Id));
    }
}
