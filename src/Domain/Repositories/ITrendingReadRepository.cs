using OsuStocks.Domain.Models.Market;

namespace OsuStocks.Domain.Repositories;

public interface ITrendingReadRepository
{
    Task<TrendingReadModel> GetTrendingAsync(
        DateTimeOffset windowStart,
        int limit,
        CancellationToken cancellationToken = default);
}
