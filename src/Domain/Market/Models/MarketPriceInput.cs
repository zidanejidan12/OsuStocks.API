namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPriceInput(
    MarketInputType Type,
    int Quantity = 0,
    decimal PreviousPp = 0m,
    decimal CurrentPp = 0m,
    decimal TopPlayPp = 0m)
{
    public static MarketPriceInput Buy(int quantity) => new(MarketInputType.BuyOrderExecuted, quantity);
    public static MarketPriceInput Sell(int quantity) => new(MarketInputType.SellOrderExecuted, quantity);

    // CurrentPp carries the player's overall pp; TopPlayPp the pp of the newly-set play. The engine
    // scales the price bump by playPp / playerPp so breakout plays move the stock more than the same
    // pp play from a top player.
    public static MarketPriceInput TopPlay(decimal playPp, decimal playerPp)
        => new(MarketInputType.TopPlayDetected, CurrentPp: playerPp, TopPlayPp: playPp);
    public static MarketPriceInput PpIncrease(decimal previousPp, decimal currentPp)
        => new(MarketInputType.PpIncreased, 0, previousPp, currentPp);
    public static MarketPriceInput Inactivity() => new(MarketInputType.PlayerInactive);
}
