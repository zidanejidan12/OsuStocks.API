namespace OsuStocks.Application.Features.Notifications.GetNotifications;

public sealed record GetNotificationsResponse(
    IReadOnlyList<NotificationItemResponse> Items,
    int Page,
    int PageSize);

public sealed record NotificationItemResponse(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Data,
    bool IsRead,
    DateTimeOffset CreatedAt);
