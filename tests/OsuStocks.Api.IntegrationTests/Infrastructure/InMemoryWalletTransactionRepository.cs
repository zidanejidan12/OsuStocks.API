using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryWalletTransactionRepository : IWalletTransactionRepository
{
    private readonly ConcurrentDictionary<Guid, WalletTransaction> _transactionsById = new();

    public int Count => _transactionsById.Count;

    public Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
    {
        _transactionsById[transaction.Id] = Clone(transaction)!;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WalletTransaction>> GetByWalletIdAsync(
        Guid walletId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var items = _transactionsById.Values
            .Where(x => x.WalletId == walletId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 0))
            .Select(Clone)
            .Cast<WalletTransaction>()
            .ToList();

        return Task.FromResult<IReadOnlyList<WalletTransaction>>(items);
    }

    public Task<IReadOnlyList<WalletTransactionReadModel>> GetProjectedByWalletIdAsync(
        Guid walletId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var items = _transactionsById.Values
            .Where(x => x.WalletId == walletId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 0))
            .Select(x => new WalletTransactionReadModel(
                x.Id,
                x.TransactionType.ToString(),
                x.Amount,
                x.ReferenceId,
                x.CreatedAt))
            .ToList();

        return Task.FromResult<IReadOnlyList<WalletTransactionReadModel>>(items);
    }

    private static WalletTransaction? Clone(WalletTransaction? transaction)
    {
        if (transaction is null)
        {
            return null;
        }

        return new WalletTransaction
        {
            Id = transaction.Id,
            WalletId = transaction.WalletId,
            TransactionType = transaction.TransactionType,
            Amount = transaction.Amount,
            ReferenceId = transaction.ReferenceId,
            CreatedAt = transaction.CreatedAt
        };
    }
}
