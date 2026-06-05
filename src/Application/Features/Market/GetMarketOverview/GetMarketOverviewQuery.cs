using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketOverview;

public sealed record GetMarketOverviewQuery : IRequest<Result<GetMarketOverviewResponse>>;
