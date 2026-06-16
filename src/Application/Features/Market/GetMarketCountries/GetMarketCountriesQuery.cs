using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketCountries;

public sealed record GetMarketCountriesQuery : IRequest<Result<GetMarketCountriesResponse>>;
