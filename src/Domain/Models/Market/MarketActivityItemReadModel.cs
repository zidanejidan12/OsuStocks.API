namespace OsuStocks.Domain.Models.Market;

public sealed record MarketActivityItemReadModel(
    Guid StockId,
    string PlayerName,
    string? AvatarUrl,
    string? CountryCode,
    string Reason,
    string Description,
    decimal PercentChange,
    decimal NewPrice,
    DateTimeOffset OccurredAt);
