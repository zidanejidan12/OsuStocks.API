using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Notifications.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid UserId)
    : IRequest<Result<int>>;
