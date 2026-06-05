namespace OsuStocks.Domain.Models.Market;

public sealed record MarketOverviewReadModel(
    int TotalStocks,
    long TotalVolume,
    MarketTopMoverReadModel? TopGainer,
    MarketTopMoverReadModel? TopLoser);
