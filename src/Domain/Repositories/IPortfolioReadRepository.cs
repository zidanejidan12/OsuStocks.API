using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface IPortfolioReadRepository
{
    Task<IReadOnlyList<PortfolioHoldingSummaryReadModel>> GetPortfolioSummaryHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HoldingReadModel>> GetHoldingsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
