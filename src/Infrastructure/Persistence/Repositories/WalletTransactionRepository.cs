using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class WalletTransactionRepository(AppDbContext dbContext) : IWalletTransactionRepository
{
    public Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
    {
        return dbContext.WalletTransactions.AddAsync(transaction, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(
        Guid walletId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(x => x.WalletId == walletId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WalletTransactionReadModel>> GetProjectedByWalletIdAsync(
        Guid walletId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(x => x.WalletId == walletId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new WalletTransactionReadModel(
                x.Id,
                x.TransactionType.ToString(),
                x.Amount,
                x.ReferenceId,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
