using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface IEconomyReadRepository
{
    Task<EconomySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
