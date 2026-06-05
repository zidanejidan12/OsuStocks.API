namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPriceCalculation(
    decimal PreviousPrice,
    decimal NewPrice,
    decimal PercentageChange);
