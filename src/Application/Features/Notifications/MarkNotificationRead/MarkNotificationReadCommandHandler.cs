using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notificationRepository,
    IApplicationDbContext dbContext)
    : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.GetByIdForUserAsync(
            request.NotificationId, request.UserId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure<bool>("NOT_FOUND", "Notification not found.");
        }

        notification.IsRead = true;
        notificationRepository.Update(notification);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
