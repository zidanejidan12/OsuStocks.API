using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _usersById = new();
    private readonly ConcurrentDictionary<long, Guid> _idsByOsuUserId = new();

    public int Count => _usersById.Count;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _usersById.TryGetValue(id, out var user);
        return Task.FromResult(Clone(user));
    }

    public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _usersById.TryGetValue(id, out var user);
        return Task.FromResult(Clone(user));
    }

    public Task<User?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        if (!_idsByOsuUserId.TryGetValue(osuUserId, out var userId))
        {
            return Task.FromResult<User?>(null);
        }

        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(Clone(user));
    }

    public Task<bool> ExistsByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_idsByOsuUserId.ContainsKey(osuUserId));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        if (!_idsByOsuUserId.TryAdd(user.OsuUserId, user.Id))
        {
            throw new InvalidOperationException($"User with osu id '{user.OsuUserId}' already exists.");
        }

        _usersById[user.Id] = Clone(user)!;
        return Task.CompletedTask;
    }

    public void Update(User user)
    {
        _idsByOsuUserId[user.OsuUserId] = user.Id;
        _usersById[user.Id] = Clone(user)!;
    }

    private static User? Clone(User? user)
    {
        if (user is null)
        {
            return null;
        }

        return new User
        {
            Id = user.Id,
            OsuUserId = user.OsuUserId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            CountryCode = user.CountryCode,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            UpdatedAt = user.UpdatedAt,
            UpdatedBy = user.UpdatedBy,
            LastLoginAt = user.LastLoginAt,
            DailyRewardStreak = user.DailyRewardStreak,
            LastDailyRewardDate = user.LastDailyRewardDate
        };
    }
}
