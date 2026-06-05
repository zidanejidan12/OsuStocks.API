using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketStocks;

public sealed record GetMarketStocksQuery(
    int Page,
    int PageSize,
    string? Sort,
    string? Search)
    : IRequest<Result<GetMarketStocksResponse>>;
