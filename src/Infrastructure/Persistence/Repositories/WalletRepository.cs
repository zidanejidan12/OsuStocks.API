using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class WalletRepository(AppDbContext dbContext) : IWalletRepository
{
    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task<WalletBalanceReadModel?> GetBalanceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Wallets
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new WalletBalanceReadModel(x.Balance))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        return dbContext.Wallets.AddAsync(wallet, cancellationToken).AsTask();
    }

    public void Update(Wallet wallet)
    {
        var entry = dbContext.Entry(wallet);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Wallets.Attach(wallet);
            entry = dbContext.Entry(wallet);
        }

        entry.State = EntityState.Modified;
        entry.Property(x => x.RowVersion).OriginalValue = wallet.RowVersion;
    }
}
