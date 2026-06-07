using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Notifications.GetNotifications;

public sealed record GetNotificationsQuery(
    Guid UserId,
    bool UnreadOnly,
    int Page,
    int PageSize)
    : IRequest<Result<GetNotificationsResponse>>;
