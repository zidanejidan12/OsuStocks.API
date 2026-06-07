using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetStockCandles;

public sealed record GetStockCandlesQuery(Guid StockId, string Range)
    : IRequest<Result<GetStockCandlesResponse>>;
