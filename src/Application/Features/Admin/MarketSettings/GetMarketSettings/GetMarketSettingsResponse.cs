namespace OsuStocks.Application.Features.Admin.MarketSettings.GetMarketSettings;

public sealed record GetMarketSettingsResponse(
    decimal PpMultiplier,
    decimal TradeMultiplier,
    decimal DecayMultiplier);
