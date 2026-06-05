using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetMarketStockDetails;

public sealed record GetMarketStockDetailsQuery(Guid StockId)
    : IRequest<Result<GetMarketStockDetailsResponse>>;
