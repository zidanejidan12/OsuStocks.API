using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Trading.BuyStock;

public sealed record BuyStockCommand(Guid UserId, Guid StockId, decimal Quantity)
    : IRequest<Result<BuyStockResponse>>;
