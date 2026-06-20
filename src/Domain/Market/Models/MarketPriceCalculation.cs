namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPriceCalculation(
    decimal PreviousPrice,
    decimal NewPrice,
    decimal PercentageChange,
    // Liquidity-based bid/ask spread for a trade (0 for non-trade price moves). The trader fills at
    // mid * (1 ± SpreadRate/2): buys above mid, sells below — wider on thin (illiquid) stocks.
    decimal SpreadRate = 0m);
