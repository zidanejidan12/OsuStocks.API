namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPriceInput(
    MarketInputType Type,
    decimal Quantity = 0m,
    decimal PreviousPp = 0m,
    decimal CurrentPp = 0m,
    decimal TopPlayPp = 0m,
    int PreviousRank = 0,
    int CurrentRank = 0,
    // Stock liquidity (float + recent volume) at trade time. Higher liquidity dampens the price
    // impact and the bid/ask spread of a trade — deep stocks move less, thin stocks swing more.
    decimal Liquidity = 0m)
{
    public static MarketPriceInput Buy(decimal quantity, decimal liquidity = 0m)
        => new(MarketInputType.BuyOrderExecuted, quantity, Liquidity: liquidity);
    public static MarketPriceInput Sell(decimal quantity, decimal liquidity = 0m)
        => new(MarketInputType.SellOrderExecuted, quantity, Liquidity: liquidity);

    // CurrentPp carries the player's overall pp; TopPlayPp the pp of the newly-set play. The engine
    // scales the price bump by playPp / playerPp so breakout plays move the stock more than the same
    // pp play from a top player.
    public static MarketPriceInput TopPlay(decimal playPp, decimal playerPp)
        => new(MarketInputType.TopPlayDetected, CurrentPp: playerPp, TopPlayPp: playPp);
    public static MarketPriceInput PpIncrease(decimal previousPp, decimal currentPp)
        => new(MarketInputType.PpIncreased, 0, previousPp, currentPp);
    public static MarketPriceInput Inactivity() => new(MarketInputType.PlayerInactive);

    // Rank is bidirectional: improving (rank number falls) lifts the price; dropping lowers it.
    public static MarketPriceInput RankChange(int previousRank, int currentRank)
        => new(MarketInputType.RankChanged, PreviousRank: previousRank, CurrentRank: currentRank);
}
