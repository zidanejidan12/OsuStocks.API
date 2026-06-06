using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Trading.Services;

public interface ITradingGuardService
{
    Task<Result> CheckCooldownAsync(
        Guid userId,
        Guid stockId,
        CancellationToken cancellationToken = default);

    Task<Result> CheckPositionLimitAsync(
        Guid userId,
        Guid stockId,
        int requestedQuantity,
        int currentHoldingQuantity,
        CancellationToken cancellationToken = default);

    Task CheckRapidTradingAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
