namespace OsuStocks.Application.Features.Market.GetMarketCountries;

public sealed record GetMarketCountriesResponse(
    IReadOnlyList<MarketCountryResponse> Items);

public sealed record MarketCountryResponse(
    string CountryCode,
    int Count);
