using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryWalletRepository : IWalletRepository
{
    private readonly ConcurrentDictionary<Guid, Wallet> _walletsById = new();
    private readonly ConcurrentDictionary<Guid, Guid> _walletIdsByUserId = new();

    public int Count => _walletsById.Count;

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _walletsById.TryGetValue(id, out var wallet);
        return Task.FromResult(Clone(wallet));
    }

    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_walletIdsByUserId.TryGetValue(userId, out var walletId))
        {
            return Task.FromResult<Wallet?>(null);
        }

        _walletsById.TryGetValue(walletId, out var wallet);
        return Task.FromResult(Clone(wallet));
    }

    public Task<Wallet?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return GetByUserIdAsync(userId, cancellationToken);
    }

    public Task<WalletBalanceReadModel?> GetBalanceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_walletIdsByUserId.TryGetValue(userId, out var walletId))
        {
            return Task.FromResult<WalletBalanceReadModel?>(null);
        }

        if (!_walletsById.TryGetValue(walletId, out var wallet))
        {
            return Task.FromResult<WalletBalanceReadModel?>(null);
        }

        return Task.FromResult<WalletBalanceReadModel?>(new WalletBalanceReadModel(wallet.Balance));
    }

    public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        if (!_walletIdsByUserId.TryAdd(wallet.UserId, wallet.Id))
        {
            throw new InvalidOperationException($"Wallet for user '{wallet.UserId}' already exists.");
        }

        _walletsById[wallet.Id] = Clone(wallet)!;
        return Task.CompletedTask;
    }

    public void Update(Wallet wallet)
    {
        _walletIdsByUserId[wallet.UserId] = wallet.Id;
        _walletsById[wallet.Id] = Clone(wallet)!;
    }

    private static Wallet? Clone(Wallet? wallet)
    {
        if (wallet is null)
        {
            return null;
        }

        return new Wallet
        {
            Id = wallet.Id,
            UserId = wallet.UserId,
            Balance = wallet.Balance,
            RowVersion = wallet.RowVersion,
            CreatedAt = wallet.CreatedAt,
            CreatedBy = wallet.CreatedBy,
            UpdatedAt = wallet.UpdatedAt,
            UpdatedBy = wallet.UpdatedBy
        };
    }
}
