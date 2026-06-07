using OsuStocks.Domain.Models;

namespace OsuStocks.Domain.Repositories;

public interface INotificationReadRepository
{
    Task<IReadOnlyList<NotificationReadModel>> GetByUserAsync(
        Guid userId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
