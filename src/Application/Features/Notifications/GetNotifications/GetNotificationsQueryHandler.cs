using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Notifications.GetNotifications;

public sealed class GetNotificationsQueryHandler(INotificationReadRepository notificationReadRepository)
    : IRequestHandler<GetNotificationsQuery, Result<GetNotificationsResponse>>
{
    public async Task<Result<GetNotificationsResponse>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await notificationReadRepository.GetByUserAsync(
            request.UserId,
            request.UnreadOnly,
            (request.Page - 1) * request.PageSize,
            request.PageSize,
            cancellationToken);

        return Result.Success(new GetNotificationsResponse(
            items.Select(x => new NotificationItemResponse(
                x.Id,
                x.Type,
                x.Title,
                x.Body,
                x.Data,
                x.IsRead,
                x.CreatedAt)).ToList(),
            request.Page,
            request.PageSize));
    }
}
