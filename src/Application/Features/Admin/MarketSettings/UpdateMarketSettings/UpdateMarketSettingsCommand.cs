using MediatR;
using OsuStocks.Application.Common.Models;

namespace OsuStocks.Application.Features.Admin.MarketSettings.UpdateMarketSettings;

public sealed record UpdateMarketSettingsCommand(
    decimal PpMultiplier,
    decimal TradeMultiplier,
    decimal DecayMultiplier,
    bool IsMaintenanceMode,
    string? Actor) : IRequest<Result<UpdateMarketSettingsResponse>>;
