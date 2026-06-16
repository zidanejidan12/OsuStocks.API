using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Market.GetMarketCountries;

public sealed class GetMarketCountriesQueryHandler(IMarketReadRepository marketReadRepository)
    : IRequestHandler<GetMarketCountriesQuery, Result<GetMarketCountriesResponse>>
{
    public async Task<Result<GetMarketCountriesResponse>> Handle(
        GetMarketCountriesQuery request,
        CancellationToken cancellationToken)
    {
        var countries = await marketReadRepository.GetCountriesAsync(cancellationToken);

        return Result.Success(new GetMarketCountriesResponse(
            countries.Select(x => new MarketCountryResponse(x.CountryCode, x.Count)).ToList()));
    }
}
