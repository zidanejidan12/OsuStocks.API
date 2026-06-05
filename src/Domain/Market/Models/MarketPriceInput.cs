namespace OsuStocks.Domain.Market.Models;

public sealed record MarketPriceInput(
    MarketInputType Type,
    int Quantity = 0,
    decimal PreviousPp = 0m,
    decimal CurrentPp = 0m)
{
    public static MarketPriceInput Buy(int quantity) => new(MarketInputType.BuyOrderExecuted, quantity);
    public static MarketPriceInput Sell(int quantity) => new(MarketInputType.SellOrderExecuted, quantity);
    public static MarketPriceInput TopPlay() => new(MarketInputType.TopPlayDetected);
    public static MarketPriceInput PpIncrease(decimal previousPp, decimal currentPp)
        => new(MarketInputType.PpIncreased, 0, previousPp, currentPp);
    public static MarketPriceInput Inactivity() => new(MarketInputType.PlayerInactive);
}
