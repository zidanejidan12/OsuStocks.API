using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
}
