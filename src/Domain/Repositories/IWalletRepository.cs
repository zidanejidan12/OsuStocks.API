using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a change-tracked wallet so the caller can mutate <see cref="Wallet.Balance"/> and persist
    /// it (with optimistic-concurrency on <see cref="Wallet.RowVersion"/>) via <see cref="Update"/> and a
    /// subsequent SaveChanges. Unlike <see cref="GetByUserIdAsync"/> this does not use AsNoTracking.
    /// </summary>
    Task<Wallet?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WalletBalanceReadModel?> GetBalanceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
}
