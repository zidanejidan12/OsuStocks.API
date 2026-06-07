using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetStockAnalytics;

public sealed record GetStockAnalyticsQuery(Guid StockId)
    : IRequest<Result<GetStockAnalyticsResponse>>;
