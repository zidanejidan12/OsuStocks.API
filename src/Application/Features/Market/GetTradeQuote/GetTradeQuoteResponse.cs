namespace OsuStocks.Application.Features.Market.GetTradeQuote;

public sealed record GetTradeQuoteResponse(
    decimal Quantity,
    decimal UnitPrice,
    decimal GrossAmount,
    decimal Fee,
    /// <summary>What the wallet pays (buy = gross + fee) or receives (sell = gross − fee).</summary>
    decimal Total,
    decimal NewPrice,
    bool IsSell);
