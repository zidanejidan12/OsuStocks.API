using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketStockHistory;

public sealed record GetMarketStockHistoryQuery(Guid StockId)
    : IRequest<Result<GetMarketStockHistoryResponse>>;
