namespace OsuStocks.Domain.Models;

public sealed record NotificationReadModel(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Data,
    bool IsRead,
    DateTimeOffset CreatedAt);
