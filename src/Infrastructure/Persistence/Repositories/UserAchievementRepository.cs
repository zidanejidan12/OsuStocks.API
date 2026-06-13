using Microsoft.EntityFrameworkCore;
using Npgsql;
using OsuStocks.Domain.Achievements.Models;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class UserAchievementRepository(AppDbContext dbContext) : IUserAchievementRepository
{
    private const string ActorName = "achievement-reward";

    public async Task<IReadOnlyList<AchievementUnlockReadModel>> GetUnlockedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserAchievements
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new AchievementUnlockReadModel(x.AchievementCode, x.UnlockedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryUnlockAndRewardAsync(
        Guid userId,
        string achievementCode,
        long rewardCredits,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        // The unlock row's unique (user, code) index is the idempotency key: insert it first, and a
        // concurrent duplicate loses with a unique-violation we treat as "already unlocked". Only on a
        // successful insert do we move money, all inside one transaction so a credited wallet always
        // has a matching ledger row.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var unlock = new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AchievementCode = achievementCode,
            RewardCredits = rewardCredits,
            UnlockedAt = occurredAt,
        };
        dbContext.UserAchievements.Add(unlock);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the race against a concurrent unlock of the same (user, code): already unlocked.
            dbContext.Entry(unlock).State = EntityState.Detached;
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await CreditWalletAsync(userId, rewardCredits, unlock.Id, WalletTransactionType.AchievementReward,
            occurredAt, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task CreditWalletAsync(
        Guid userId,
        long rewardCredits,
        Guid referenceId,
        WalletTransactionType transactionType,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (rewardCredits <= 0L)
        {
            return;
        }

        var walletId = await dbContext.Wallets
            .Where(w => w.UserId == userId)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (walletId is null)
        {
            return;
        }

        // Atomic SET balance = balance + reward, so concurrent reward grants never lose credits.
        await dbContext.Wallets
            .Where(w => w.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.Balance, w => w.Balance + rewardCredits)
                .SetProperty(w => w.RowVersion, w => w.RowVersion + 1)
                .SetProperty(w => w.UpdatedAt, occurredAt)
                .SetProperty(w => w.UpdatedBy, ActorName),
                cancellationToken);

        // ExecuteUpdate bypasses the change tracker; detach any tracked copy of this wallet (the
        // originating trade tracked one in the shared scoped context) so its now-stale balance /
        // row_version can never be written back by a later save in the same request scope.
        DetachTrackedWallet(userId);

        dbContext.WalletTransactions.Add(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId.Value,
            TransactionType = transactionType,
            Amount = rewardCredits,
            ReferenceId = referenceId,
            CreatedAt = occurredAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void DetachTrackedWallet(Guid userId)
    {
        var tracked = dbContext.ChangeTracker.Entries<Wallet>()
            .FirstOrDefault(e => e.Entity.UserId == userId);
        if (tracked is not null)
        {
            tracked.State = EntityState.Detached;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
