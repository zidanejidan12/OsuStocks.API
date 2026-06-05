namespace OsuStocks.Domain.Market.Models;

public enum MarketInputType
{
    BuyOrderExecuted = 1,
    SellOrderExecuted = 2,
    TopPlayDetected = 3,
    PpIncreased = 4,
    PlayerInactive = 5
}
