using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
}
