using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
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

    public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        return dbContext.Wallets.AddAsync(wallet, cancellationToken).AsTask();
    }

    public void Update(Wallet wallet)
    {
        dbContext.Wallets.Update(wallet);
    }
}
