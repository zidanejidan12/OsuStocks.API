namespace OsuStocks.Domain.Models;

public sealed record HoldingReadModel(
    Guid HoldingId,
    Guid StockId,
    string? PlayerName,
    int Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string? AvatarUrl = null);
