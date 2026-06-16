using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.DailyLogin.Services;

public sealed class DailyLoginRewardService(
    IUserRepository userRepository,
    IWalletRepository walletRepository,
    IWalletTransactionRepository walletTransactionRepository,
    IDailyLoginRewardRepository dailyLoginRewardRepository,
    IDailyRewardSettings settings)
    : IDailyLoginRewardService
{
    private const string Actor = "daily-login";

    public async Task<Result<DailyRewardGrantResult>> GrantDailyRewardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var schedule = settings.DailyAmounts;
        if (schedule is null || schedule.Count == 0)
        {
            return Result.Failure<DailyRewardGrantResult>(
                "CONFIGURATION_ERROR", "Daily reward schedule is not configured.");
        }

        var today = DailyLoginClock.ServerToday();

        // A single read of the most recent ledger row serves BOTH the already-claimed fast path and the
        // streak computation below. A reward is only ever granted for the current day, so a row can never be
        // dated in the future — meaning "the latest row is today" is exactly "already claimed today". This is
        // an optimization only; the authoritative guard remains the unique (user_id, reward_date) index
        // enforced at commit time.
        var latest = await dailyLoginRewardRepository.GetLatestByUserAsync(userId, cancellationToken);
        if (latest is not null && latest.RewardDate == today)
        {
            return Result.Success(new DailyRewardGrantResult(
                Granted: false,
                AlreadyClaimed: true,
                Amount: latest.Amount,
                StreakDay: latest.StreakDay,
                NewBalance: await ReadBalanceAsync(userId, cancellationToken)));
        }

        var user = await userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<DailyRewardGrantResult>("NOT_FOUND", "User not found.");
        }

        var wallet = await walletRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
        if (wallet is null)
        {
            return Result.Failure<DailyRewardGrantResult>("NOT_FOUND", "Wallet not found.");
        }

        var streak = DailyRewardStreakCalculator.Compute(
            latest?.RewardDate, latest?.StreakDay ?? 0, today, schedule.Count);

        if (streak.AlreadyClaimed)
        {
            // Defensive: the fast path above already returns for a today-dated latest row, so this is
            // unreachable in practice — but guarding here prevents a future change from indexing schedule[-1].
            return Result.Success(new DailyRewardGrantResult(
                Granted: false,
                AlreadyClaimed: true,
                Amount: latest!.Amount,
                StreakDay: latest.StreakDay,
                NewBalance: await ReadBalanceAsync(userId, cancellationToken)));
        }

        var amount = schedule[streak.StreakDay - 1];
        var now = DateTimeOffset.UtcNow;
        var rewardId = Guid.NewGuid();

        var reward = new DailyLoginReward
        {
            Id = rewardId,
            UserId = userId,
            RewardDate = today,
            StreakDay = streak.StreakDay,
            Amount = amount,
            CreatedAt = now
        };
        await dailyLoginRewardRepository.AddAsync(reward, cancellationToken);

        wallet.Balance += amount;
        wallet.UpdatedAt = now;
        wallet.UpdatedBy = Actor;
        walletRepository.Update(wallet);

        // The link to the ledger row uses the existing WalletTransaction.ReferenceId convention (as
        // BuyStock/SellStock do); it must be set at insert time because wallet_transactions are immutable.
        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionType = WalletTransactionType.DailyReward,
            Amount = amount,
            ReferenceId = rewardId,
            CreatedAt = now
        };
        await walletTransactionRepository.AddAsync(walletTransaction, cancellationToken);

        user.DailyRewardStreak = streak.StreakDay;
        user.LastDailyRewardDate = today;
        user.UpdatedAt = now;
        user.UpdatedBy = Actor;
        userRepository.Update(user);

        // Single unit of work: ledger insert + wallet credit + transaction + user cache commit atomically.
        var committed = await dailyLoginRewardRepository.TryCommitClaimAsync(cancellationToken);
        if (!committed)
        {
            // Lost the per-day race: the unique index rejected the insert and the whole batch rolled back,
            // so nothing was persisted. Report the winning grant.
            var winner = await dailyLoginRewardRepository.GetByUserAndDateAsync(userId, today, cancellationToken);
            return Result.Success(new DailyRewardGrantResult(
                Granted: false,
                AlreadyClaimed: true,
                Amount: winner?.Amount ?? amount,
                StreakDay: winner?.StreakDay ?? streak.StreakDay,
                NewBalance: await ReadBalanceAsync(userId, cancellationToken)));
        }

        return Result.Success(new DailyRewardGrantResult(
            Granted: true,
            AlreadyClaimed: false,
            Amount: amount,
            StreakDay: streak.StreakDay,
            NewBalance: wallet.Balance));
    }

    private async Task<decimal> ReadBalanceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var balance = await walletRepository.GetBalanceByUserIdAsync(userId, cancellationToken);
        return balance?.Balance ?? 0m;
    }
}
