using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Notifications.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId)
    : IRequest<Result<bool>>;
