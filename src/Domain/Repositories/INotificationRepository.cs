using OsuStocks.Domain.Entities;

namespace OsuStocks.Domain.Repositories;

public interface INotificationRepository
{
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    void Update(Notification notification);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetHolderUserIdsByStockIdAsync(Guid stockId, CancellationToken cancellationToken = default);
}
