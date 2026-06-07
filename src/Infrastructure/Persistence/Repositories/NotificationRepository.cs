using Microsoft.EntityFrameworkCore;
using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    public Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);
    }

    public void Update(Notification notification)
    {
        var entry = dbContext.Entry(notification);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Notifications.Attach(notification);
            entry = dbContext.Entry(notification);
        }

        entry.State = EntityState.Modified;
    }

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetHolderUserIdsByStockIdAsync(
        Guid stockId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from holding in dbContext.Holdings.AsNoTracking()
            join portfolio in dbContext.Portfolios.AsNoTracking()
                on holding.PortfolioId equals portfolio.Id
            where holding.StockId == stockId && holding.Quantity > 0
            select portfolio.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
