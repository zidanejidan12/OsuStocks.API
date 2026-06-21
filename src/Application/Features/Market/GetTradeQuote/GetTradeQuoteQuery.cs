using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Market.GetTradeQuote;

/// <summary>
/// A pre-trade estimate for buying/selling <paramref name="Quantity"/> shares of a stock: the exact
/// fill price (slippage + spread), the progressive service fee, and the resulting wallet debit (buy)
/// or net proceeds (sell). Uses the same engine + fee policy as a real trade, so it's accurate.
/// </summary>
public sealed record GetTradeQuoteQuery(Guid StockId, decimal Quantity, bool IsSell)
    : IRequest<Result<GetTradeQuoteResponse>>;
