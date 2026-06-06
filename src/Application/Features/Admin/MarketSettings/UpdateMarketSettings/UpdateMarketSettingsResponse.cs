namespace OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;

public sealed record UpdateMarketSettingsResponse(
    decimal PpMultiplier,
    decimal TradeMultiplier,
    decimal DecayMultiplier,
    bool IsMaintenanceMode);
