using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class NotificationReadRepository(AppDbContext dbContext) : INotificationReadRepository
{
    public async Task<IReadOnlyList<NotificationReadModel>> GetByUserAsync(
        Guid userId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new NotificationReadModel(
                x.Id,
                x.Type,
                x.Title,
                x.Body,
                x.Data,
                x.IsRead,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
