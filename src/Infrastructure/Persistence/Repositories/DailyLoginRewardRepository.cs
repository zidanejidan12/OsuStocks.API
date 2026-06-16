using Microsoft.EntityFrameworkCore;
using Npgsql;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class DailyLoginRewardRepository(AppDbContext dbContext) : IDailyLoginRewardRepository
{
    /// <summary>Name of the unique (user_id, reward_date) index — the per-day idempotency guard.</summary>
    public const string UniqueConstraintName = "uq_daily_login_rewards_user_date";

    public Task AddAsync(DailyLoginReward reward, CancellationToken cancellationToken = default)
    {
        return dbContext.DailyLoginRewards.AddAsync(reward, cancellationToken).AsTask();
    }

    public Task<DailyLoginReward?> GetByUserAndDateAsync(
        Guid userId,
        DateOnly rewardDate,
        CancellationToken cancellationToken = default)
    {
        return dbContext.DailyLoginRewards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RewardDate == rewardDate, cancellationToken);
    }

    public Task<DailyLoginReward?> GetLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.DailyLoginRewards
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RewardDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryCommitClaimAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsDailyRewardDuplicate(ex))
        {
            // Another request already claimed this (user, day). The whole batch rolled back atomically, so
            // nothing was persisted. Everything else — FK/not-null/trigger violations, and notably
            // DbUpdateConcurrencyException — falls through and propagates so real defects are not masked.
            return false;
        }
    }

    private static bool IsDailyRewardDuplicate(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == UniqueConstraintName;
    }
}
