using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Application.Features.Admin.TransactionMonitor.GetAdminTrades;

public sealed record GetAdminTradesQuery(
    Guid? UserId = null,
    Guid? StockId = null,
    TradeType? TradeType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 25) : IRequest<Result<GetAdminTradesResponse>>;
