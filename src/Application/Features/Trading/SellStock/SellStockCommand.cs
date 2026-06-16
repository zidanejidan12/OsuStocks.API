using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Trading.SellStock;

public sealed record SellStockCommand(Guid UserId, Guid StockId, decimal Quantity)
    : IRequest<Result<SellStockResponse>>;
