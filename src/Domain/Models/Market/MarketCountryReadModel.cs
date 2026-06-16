namespace OsuStocks.Domain.Models.Market;

public sealed record MarketCountryReadModel(
    string CountryCode,
    int Count);
