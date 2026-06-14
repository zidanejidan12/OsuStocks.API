using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetStockTopPlays;

public sealed record GetStockTopPlaysQuery(
    Guid StockId,
    int Limit)
    : IRequest<Result<GetStockTopPlaysResponse>>;
