using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a change-tracked user so the caller can mutate it and persist the changes via
    /// <see cref="Update"/> and a subsequent SaveChanges. Unlike <see cref="GetByIdAsync"/> this does not
    /// use AsNoTracking.
    /// </summary>
    Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
}
