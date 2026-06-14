using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetLiveMovers;

public sealed record GetLiveMoversQuery(int Limit) : IRequest<Result<GetLiveMoversResponse>>;
