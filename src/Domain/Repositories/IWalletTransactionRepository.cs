using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IWalletTransactionRepository
{
    Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(
        Guid walletId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
