using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryDailyLoginRewardRepository : IDailyLoginRewardRepository
{
    private readonly ConcurrentDictionary<(Guid UserId, DateOnly RewardDate), DailyLoginReward> _committed = new();
    private readonly List<DailyLoginReward> _pending = new();
    private readonly object _gate = new();

    public int Count => _committed.Count;

    public Task AddAsync(DailyLoginReward reward, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _pending.Add(Clone(reward)!);
        }

        return Task.CompletedTask;
    }

    public Task<DailyLoginReward?> GetByUserAndDateAsync(
        Guid userId,
        DateOnly rewardDate,
        CancellationToken cancellationToken = default)
    {
        _committed.TryGetValue((userId, rewardDate), out var reward);
        return Task.FromResult(Clone(reward));
    }

    public Task<DailyLoginReward?> GetLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var latest = _committed.Values
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RewardDate)
            .FirstOrDefault();

        return Task.FromResult(Clone(latest));
    }

    public Task<bool> TryCommitClaimAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Mirror the database unique (user_id, reward_date) constraint: commit the staged rows only if
            // none would collide with an already-committed day.
            if (_pending.Any(p => _committed.ContainsKey((p.UserId, p.RewardDate))))
            {
                _pending.Clear();
                return Task.FromResult(false);
            }

            foreach (var pending in _pending)
            {
                _committed[(pending.UserId, pending.RewardDate)] = pending;
            }

            _pending.Clear();
            return Task.FromResult(true);
        }
    }

    private static DailyLoginReward? Clone(DailyLoginReward? reward)
    {
        if (reward is null)
        {
            return null;
        }

        return new DailyLoginReward
        {
            Id = reward.Id,
            UserId = reward.UserId,
            RewardDate = reward.RewardDate,
            StreakDay = reward.StreakDay,
            Amount = reward.Amount,
            CreatedAt = reward.CreatedAt
        };
    }
}
