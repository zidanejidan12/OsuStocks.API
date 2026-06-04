using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.OsuUserId == osuUserId, cancellationToken);
    }

    public Task<bool> ExistsByOsuUserIdAsync(long osuUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AsNoTracking().AnyAsync(x => x.OsuUserId == osuUserId, cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public void Update(User user)
    {
        dbContext.Users.Update(user);
    }
}
